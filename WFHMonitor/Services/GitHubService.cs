using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public class GitHubSettings
{
    public string Token { get; set; } = string.Empty;
}

public record GitHubCommit(
    string Sha,
    string Message,
    string AuthorName,
    string AuthorEmail,
    DateTime Date,
    string Url
);

public record GitHubRepositoryInfo(
    string Name,
    string FullName,
    string HtmlUrl,
    string DefaultBranch,
    string? Description,
    string? Homepage
);

public record GitHubConnectedRepository(
    string Name,
    string FullName,
    string OwnerLogin,
    string HtmlUrl,
    string DefaultBranch,
    bool IsPrivate,
    string? Description
);

public class GitHubService : IGitHubService
{
    private readonly HttpClient _http;
    private readonly string _token;
    private readonly IGitHubOAuthService _gitHubOAuthService;

    public GitHubService(
        HttpClient http,
        IOptions<GitHubSettings> settings,
        IGitHubOAuthService gitHubOAuthService)
    {
        _http = http;
        _token = settings.Value.Token;
        _gitHubOAuthService = gitHubOAuthService;
    }

    public async Task<List<GitHubCommit>> GetCommitsAsync(string owner, string repo, string branch, int count = 10)
    {
        var json = await SendGitHubGetAsync(
            $"https://api.github.com/repos/{owner}/{repo}/commits?sha={branch}&per_page={count}");
        using var doc = JsonDocument.Parse(json);

        var commits = new List<GitHubCommit>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var sha = item.GetProperty("sha").GetString() ?? "";
            var commitNode = item.GetProperty("commit");
            var message = commitNode.GetProperty("message").GetString() ?? "";
            var authorNode = commitNode.GetProperty("author");
            var authorName = authorNode.GetProperty("name").GetString() ?? "";
            var authorEmail = authorNode.GetProperty("email").GetString() ?? "";
            var dateStr = authorNode.GetProperty("date").GetString() ?? "";
            DateTime.TryParse(dateStr, out var date);
            var url = item.GetProperty("html_url").GetString() ?? "";

            // Take only first line of commit message
            var firstLine = message.Split('\n')[0].Trim();

            commits.Add(new GitHubCommit(sha[..Math.Min(7, sha.Length)], firstLine, authorName, authorEmail, date, url));
        }
        return commits;
    }

    public async Task<List<string>> GetRepoTreeAsync(string owner, string repo, string branch)
    {
        var json = await SendGitHubGetAsync(
            $"https://api.github.com/repos/{owner}/{repo}/git/trees/{branch}?recursive=1");
        using var doc = JsonDocument.Parse(json);

        var paths = new List<string>();
        if (doc.RootElement.TryGetProperty("tree", out var tree))
        {
            foreach (var item in tree.EnumerateArray())
            {
                var path = item.GetProperty("path").GetString() ?? "";
                paths.Add(path);
            }
        }
        return paths;
    }

    public async Task<GitHubRepositoryInfo> GetRepositoryInfoAsync(string owner, string repo)
    {
        var json = await SendGitHubGetAsync(
            $"https://api.github.com/repos/{owner}/{repo}");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new GitHubRepositoryInfo(
            root.GetProperty("name").GetString() ?? repo,
            root.GetProperty("full_name").GetString() ?? $"{owner}/{repo}",
            root.GetProperty("html_url").GetString() ?? $"https://github.com/{owner}/{repo}",
            root.GetProperty("default_branch").GetString() ?? "main",
            root.GetProperty("description").GetString(),
            root.GetProperty("homepage").GetString()
        );
    }

    public async Task<string?> GetReadmeContentAsync(string owner, string repo)
    {
        var request = await CreateRequestAsync(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/readme");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        var response = await _http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"GitHub API {(int)response.StatusCode}: {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("content", out var contentElement))
            return null;

        var encoded = contentElement.GetString();
        if (string.IsNullOrWhiteSpace(encoded))
            return null;

        var normalized = encoded.Replace("\n", string.Empty).Replace("\r", string.Empty);
        try
        {
            var bytes = Convert.FromBase64String(normalized);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Dictionary<string, long>> GetRepositoryLanguagesAsync(string owner, string repo)
    {
        var json = await SendGitHubGetAsync(
            $"https://api.github.com/repos/{owner}/{repo}/languages");

        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt64(out var bytes))
            {
                result[prop.Name] = bytes;
            }
        }

        return result;
    }

    public async Task<List<GitHubConnectedRepository>> GetCurrentUserRepositoriesAsync(int maxCount = 200)
    {
        if (maxCount <= 0)
            return new List<GitHubConnectedRepository>();

        var oauthToken = await _gitHubOAuthService.GetCurrentUserAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(oauthToken))
            throw new Exception("Connect your GitHub account first.");

        var repositories = new List<GitHubConnectedRepository>();
        var page = 1;
        const int pageSize = 100;
        const int maxPages = 10;

        while (repositories.Count < maxCount && page <= maxPages)
        {
            var remaining = maxCount - repositories.Count;
            var requestedPageSize = Math.Max(1, Math.Min(pageSize, remaining));
            var requestUrl =
                $"https://api.github.com/user/repos?visibility=all&affiliation=owner,collaborator,organization_member&sort=updated&per_page={requestedPageSize}&page={page}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.UserAgent.ParseAdd("WFHMonitor/1.0");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oauthToken);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"GitHub API {(int)response.StatusCode}: {error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                break;

            var countInPage = 0;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var nameEl)
                    ? nameEl.GetString()
                    : null;
                var fullName = item.TryGetProperty("full_name", out var fullNameEl)
                    ? fullNameEl.GetString()
                    : null;
                var htmlUrl = item.TryGetProperty("html_url", out var htmlUrlEl)
                    ? htmlUrlEl.GetString()
                    : null;
                var defaultBranch = item.TryGetProperty("default_branch", out var branchEl)
                    ? branchEl.GetString()
                    : null;
                var isPrivate = item.TryGetProperty("private", out var privateEl) && privateEl.ValueKind == JsonValueKind.True;
                var description = item.TryGetProperty("description", out var descEl)
                    ? descEl.GetString()
                    : null;

                string ownerLogin = string.Empty;
                if (item.TryGetProperty("owner", out var ownerEl) &&
                    ownerEl.ValueKind == JsonValueKind.Object &&
                    ownerEl.TryGetProperty("login", out var loginEl))
                {
                    ownerLogin = loginEl.GetString() ?? string.Empty;
                }

                if (string.IsNullOrWhiteSpace(name) ||
                    string.IsNullOrWhiteSpace(fullName) ||
                    string.IsNullOrWhiteSpace(htmlUrl))
                {
                    continue;
                }

                repositories.Add(new GitHubConnectedRepository(
                    name,
                    fullName,
                    ownerLogin,
                    htmlUrl,
                    string.IsNullOrWhiteSpace(defaultBranch) ? "main" : defaultBranch,
                    isPrivate,
                    description
                ));

                countInPage++;
                if (repositories.Count >= maxCount)
                    break;
            }

            if (countInPage == 0 || countInPage < requestedPageSize)
                break;

            page++;
        }

        return repositories
            .OrderBy(r => r.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<string> SendGitHubGetAsync(string url)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, url);
        using var response = await _http.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            using var publicRequest = new HttpRequestMessage(HttpMethod.Get, url);
            publicRequest.Headers.UserAgent.ParseAdd("WFHMonitor/1.0");
            publicRequest.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var publicResponse = await _http.SendAsync(publicRequest);
            if (publicResponse.IsSuccessStatusCode)
                return await publicResponse.Content.ReadAsStringAsync();
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"GitHub API {(int)response.StatusCode}: {error}");
        }

        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.UserAgent.ParseAdd("WFHMonitor/1.0");

        var oauthToken = await _gitHubOAuthService.GetCurrentUserAccessTokenAsync();
        var tokenToUse = !string.IsNullOrWhiteSpace(oauthToken) ? oauthToken : _token;

        if (!string.IsNullOrWhiteSpace(tokenToUse))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenToUse);

        return request;
    }
}
