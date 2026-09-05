using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public sealed class RepositorySnapshotService : IRepositorySnapshotService
{
    private const long MaxArchiveBytes = 100L * 1024 * 1024;
    private const long MaxExtractedBytes = 500L * 1024 * 1024;
    private const int MaxEntries = 25_000;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IGitHubOAuthService _oauthService;
    private readonly GitHubSettings _settings;
    private readonly ILogger<RepositorySnapshotService> _logger;
    private readonly string _snapshotRoot;

    public RepositorySnapshotService(
        IHttpClientFactory httpClientFactory,
        IGitHubOAuthService oauthService,
        IOptions<GitHubSettings> settings,
        ILogger<RepositorySnapshotService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _oauthService = oauthService;
        _settings = settings.Value ?? new GitHubSettings();
        _logger = logger;
        _snapshotRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "Softracker",
            "RepositoryScans"));
    }

    public async Task<RepositorySnapshot> CreateAsync(
        int projectId,
        string owner,
        string repository,
        string branch,
        string requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        ValidateSegment(owner, nameof(owner));
        ValidateSegment(repository, nameof(repository));
        if (string.IsNullOrWhiteSpace(branch))
            throw new ArgumentException("Repository branch is required.", nameof(branch));

        Directory.CreateDirectory(_snapshotRoot);
        CleanupAbandonedSnapshots();

        var snapshotDirectory = Path.Combine(
            _snapshotRoot,
            $"project-{projectId}-{Guid.NewGuid():N}");
        var extractDirectory = Path.Combine(snapshotDirectory, "repository");
        var archivePath = Path.Combine(snapshotDirectory, "repository.zip");
        Directory.CreateDirectory(extractDirectory);

        try
        {
            var token = await ResolveTokenAsync(requestedByUserId);
            var commitSha = await ResolveCommitShaAsync(owner, repository, branch, token, cancellationToken);
            await DownloadArchiveAsync(owner, repository, branch, token, archivePath, cancellationToken);
            ExtractArchiveSafely(archivePath, extractDirectory);
            File.Delete(archivePath);

            var children = Directory.GetDirectories(extractDirectory);
            var rootPath = children.Length == 1 &&
                Directory.GetFiles(extractDirectory).Length == 0
                    ? children[0]
                    : extractDirectory;

            return new RepositorySnapshot(rootPath, commitSha, snapshotDirectory);
        }
        catch
        {
            DeleteDirectorySafely(snapshotDirectory);
            throw;
        }
    }

    public Task DeleteAsync(RepositorySnapshot snapshot)
    {
        DeleteDirectorySafely(snapshot.SnapshotDirectory);
        return Task.CompletedTask;
    }

    private async Task<string?> ResolveTokenAsync(string userId)
    {
        var oauthToken = string.IsNullOrWhiteSpace(userId)
            ? null
            : await _oauthService.GetUserAccessTokenAsync(userId);
        return !string.IsNullOrWhiteSpace(oauthToken)
            ? oauthToken
            : string.IsNullOrWhiteSpace(_settings.Token) ? null : _settings.Token.Trim();
    }

    private async Task<string> ResolveCommitShaAsync(
        string owner,
        string repository,
        string branch,
        string? token,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/commits/{Uri.EscapeDataString(branch)}";
        using var response = await SendWithPublicFallbackAsync(url, token, HttpCompletionOption.ResponseContentRead, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(BuildGitHubError(response.StatusCode, payload));

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("sha", out var shaElement)
            ? shaElement.GetString() ?? string.Empty
            : string.Empty;
    }

    private async Task DownloadArchiveAsync(
        string owner,
        string repository,
        string branch,
        string? token,
        string archivePath,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/zipball/{Uri.EscapeDataString(branch)}";
        using var response = await SendWithPublicFallbackAsync(url, token, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(BuildGitHubError(response.StatusCode, payload));
        }

        if (response.Content.Headers.ContentLength is > MaxArchiveBytes)
            throw new InvalidOperationException("Repository archive is larger than the 100 MB scan limit.");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(
            archivePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > MaxArchiveBytes)
                throw new InvalidOperationException("Repository archive exceeded the 100 MB scan limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private async Task<HttpResponseMessage> SendWithPublicFallbackAsync(
        string url,
        string? token,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(url, token, completionOption, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) ||
            string.IsNullOrWhiteSpace(token))
        {
            return response;
        }

        response.Dispose();
        return await SendAsync(url, null, completionOption, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        string url,
        string? token,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("Softracker/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _httpClientFactory.CreateClient().SendAsync(request, completionOption, cancellationToken);
    }

    private static void ExtractArchiveSafely(string archivePath, string destinationDirectory)
    {
        var destinationRoot = Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaxEntries)
            throw new InvalidOperationException($"Repository contains more than {MaxEntries:N0} archive entries.");

        long totalExtractedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            if (IsSymbolicLink(entry))
                continue;

            totalExtractedBytes += entry.Length;
            if (totalExtractedBytes > MaxExtractedBytes)
                throw new InvalidOperationException("Extracted repository exceeded the 500 MB scan limit.");

            var destinationPath = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Repository archive contains an unsafe path.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: false);
        }
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry) =>
        ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000;

    private void CleanupAbandonedSnapshots()
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(_snapshotRoot))
            {
                if (Directory.GetLastWriteTimeUtc(directory) < DateTime.UtcNow.AddHours(-24))
                    DeleteDirectorySafely(directory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to clean one or more abandoned repository snapshots.");
        }
    }

    private void DeleteDirectorySafely(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        var resolved = Path.GetFullPath(directory);
        var allowedPrefix = _snapshotRoot + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(resolved, _snapshotRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to delete a path outside the repository scan directory.");
        }

        if (Directory.Exists(resolved))
            Directory.Delete(resolved, recursive: true);
    }

    private static void ValidateSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            value.Contains('/') || value.Contains('\\'))
        {
            throw new ArgumentException("Invalid GitHub repository identifier.", parameterName);
        }
    }

    private static string BuildGitHubError(HttpStatusCode statusCode, string payload)
    {
        var detail = payload.Length > 300 ? payload[..300] : payload;
        return $"GitHub API {(int)statusCode}: {detail}";
    }
}
