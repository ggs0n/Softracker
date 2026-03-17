using System.Threading.Channels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public sealed class FeatureAgentQueueService : BackgroundService, IFeatureAgentQueueService
{
    private readonly Channel<FeatureAgentQueueItem> _queue = Channel.CreateUnbounded<FeatureAgentQueueItem>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FeatureAgentQueueService> _logger;
    private readonly OpenClawSettings _openClawSettings;
    private readonly IWebHostEnvironment _environment;

    public FeatureAgentQueueService(
        IServiceScopeFactory scopeFactory,
        ILogger<FeatureAgentQueueService> logger,
        IOptions<OpenClawSettings> openClawSettings,
        IWebHostEnvironment environment)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _openClawSettings = openClawSettings.Value ?? new OpenClawSettings();
        _environment = environment;
    }

    public Task EnqueueAsync(FeatureAgentQueueItem item, CancellationToken cancellationToken = default)
    {
        if (item.FeatureId <= 0 || string.IsNullOrWhiteSpace(item.FeatureAgentId) || string.IsNullOrWhiteSpace(item.AssignedAgentUserId))
            throw new ArgumentException("Invalid feature agent queue item.");

        return _queue.Writer.WriteAsync(item, cancellationToken).AsTask();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessItemAsync(item, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Feature agent queue failed for feature {FeatureId}.", item.FeatureId);
            }
        }
    }

    private async Task ProcessItemAsync(FeatureAgentQueueItem item, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var openClaw = scope.ServiceProvider.GetRequiredService<IOpenClawBugScanService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var systemSettings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();

        var feature = await db.ProjectFeatures
            .Include(f => f.ChangeRequest)
            .FirstOrDefaultAsync(f => f.Id == item.FeatureId, cancellationToken);
        if (feature == null)
            return;

        var settings = await systemSettings.GetProVersionSettingsAsync();
        if (!settings.EnableOpenClawAgents)
        {
            feature.AgentStatus = FeatureAgentStatus.Failed;
            feature.AgentLastRunAt = DateTime.UtcNow;
            feature.AgentImplementationPlan = AppendTextWithLimit(
                feature.AgentImplementationPlan,
                $"[OpenClaw Feature Run Failed - {item.FeatureAgentId} - {DateTime.UtcNow:yyyy-MM-dd HH:mm UTC}]\nOpenClaw agents are temporarily disabled by admin.",
                4000);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        feature.AssignedDeveloperId = item.AssignedAgentUserId;
        feature.AgentStatus = FeatureAgentStatus.InProgress;
        feature.AgentLastRunAt = DateTime.UtcNow;
        if (feature.Status == CrStatus.Draft)
            feature.Status = CrStatus.InProgress;

        await db.SaveChangesAsync(cancellationToken);

        var result = await openClaw.ImplementFeatureAsync(feature, feature.ChangeRequest, item.FeatureAgentId, cancellationToken);
        if (!result.Succeeded)
        {
            feature.AgentStatus = FeatureAgentStatus.Failed;
            feature.AgentLastRunAt = DateTime.UtcNow;
            feature.AgentImplementationPlan = AppendTextWithLimit(
                feature.AgentImplementationPlan,
                $"[OpenClaw Feature Run Failed - {item.FeatureAgentId} - {DateTime.UtcNow:yyyy-MM-dd HH:mm UTC}]\n{result.Error}",
                4000);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        feature.AgentStatus = FeatureAgentStatus.PrRaised;
        feature.AgentLastRunAt = DateTime.UtcNow;

        var detectedPrLink = ExtractPullRequestUrl(result.ImplementationPlan);
        if (!string.IsNullOrWhiteSpace(detectedPrLink))
            feature.PullRequestUrl = detectedPrLink;

        feature.AgentImplementationPlan = AppendTextWithLimit(
            feature.AgentImplementationPlan,
            $"[OpenClaw Feature Plan - {item.FeatureAgentId} - {DateTime.UtcNow:yyyy-MM-dd HH:mm UTC}]\n{result.ImplementationPlan.Trim()}",
            4000);
        var importedScreenshotCount = await TryImportFeatureScreenshotsAsync(
            db,
            feature,
            result.ImplementationPlan,
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await NotifyAdminsPrRaisedAsync(userManager, notificationService, feature, item.FeatureAgentId, cancellationToken);

        if (importedScreenshotCount > 0)
        {
            _logger.LogInformation(
                "Imported {Count} feature screenshot(s) for feature {FeatureNumber} ({FeatureId}).",
                importedScreenshotCount,
                feature.FeatureNumber,
                feature.Id);
        }
    }

    private static string AppendTextWithLimit(string? existing, string addition, int maxLength)
    {
        var current = (existing ?? string.Empty).Trim();
        var next = string.IsNullOrWhiteSpace(current)
            ? addition.Trim()
            : $"{current}\n\n{addition.Trim()}";

        if (next.Length <= maxLength)
            return next;

        return next[^maxLength..].Trim();
    }

    private async Task NotifyAdminsPrRaisedAsync(
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        ProjectFeature feature,
        string featureAgentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var admins = await userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count == 0)
                return;

            var title = $"Agent PR raised: {feature.FeatureNumber}";
            var message = $"{feature.FeatureNumber} moved to PRRaised by agent '{featureAgentId}'.";
            var linkUrl = $"/ChangeRequest/FeatureDetails?featureId={feature.Id}";

            foreach (var admin in admins)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await notificationService.CreateAsync(admin.Id, title, message, linkUrl);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to create PRRaised admin notifications for feature {FeatureId}.", feature.Id);
        }
    }

    private async Task<int> TryImportFeatureScreenshotsAsync(
        ApplicationDbContext db,
        ProjectFeature feature,
        string implementationPlan,
        CancellationToken cancellationToken)
    {
        if (feature.Id <= 0)
            return 0;

        var candidates = GetCandidateScreenshotPaths(implementationPlan);
        candidates.AddRange(GetConfiguredScreenshotFiles());

        if (candidates.Count == 0)
            return 0;

        var existingFileNames = await db.FeatureScreenshots
            .AsNoTracking()
            .Where(s => s.ProjectFeatureId == feature.Id)
            .Select(s => s.OriginalFileName)
            .ToListAsync(cancellationToken);

        var existingSet = existingFileNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "features", "screenshots");
        Directory.CreateDirectory(uploadFolder);

        var imported = 0;
        var seenSourceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var maxFiles = Math.Clamp(_openClawSettings.FeatureScreenshotImportMaxFiles, 1, 20);

        foreach (var source in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (imported >= maxFiles)
                break;

            if (string.IsNullOrWhiteSpace(source) || !seenSourceFiles.Add(source))
                continue;

            if (!File.Exists(source))
                continue;

            var originalName = Path.GetFileName(source);
            if (string.IsNullOrWhiteSpace(originalName) || existingSet.Contains(originalName))
                continue;

            var ext = Path.GetExtension(originalName);
            if (!IsSupportedImageExtension(ext))
                continue;

            var storedName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var destination = Path.Combine(uploadFolder, storedName);
            File.Copy(source, destination, overwrite: false);

            db.FeatureScreenshots.Add(new FeatureScreenshot
            {
                ProjectFeatureId = feature.Id,
                FileName = storedName,
                OriginalFileName = originalName
            });

            existingSet.Add(originalName);
            imported++;
        }

        return imported;
    }

    private List<string> GetConfiguredScreenshotFiles()
    {
        var folders = new List<string>();

        if (!string.IsNullOrWhiteSpace(_openClawSettings.FeatureScreenshotImportDirectory))
            folders.Add(_openClawSettings.FeatureScreenshotImportDirectory.Trim());

        if (_openClawSettings.FeatureScreenshotImportDirectories is not null)
        {
            folders.AddRange(_openClawSettings.FeatureScreenshotImportDirectories
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim()));
        }

        folders.AddRange(GetAutoOpenClawScreenshotFolders());

        var uniqueFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolvedFolders = new List<string>();
        foreach (var folder in folders)
        {
            var resolved = ResolvePath(folder);
            if (!string.IsNullOrWhiteSpace(resolved) && uniqueFolders.Add(resolved))
                resolvedFolders.Add(resolved);
        }

        if (resolvedFolders.Count == 0)
            return [];

        var lookbackMinutes = Math.Clamp(_openClawSettings.FeatureScreenshotImportLookbackMinutes, 1, 24 * 60);
        var minWriteTime = DateTime.UtcNow.AddMinutes(-lookbackMinutes);

        return resolvedFolders
            .Where(Directory.Exists)
            .SelectMany(folder =>
            {
                try
                {
                    return Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories);
                }
                catch
                {
                    return [];
                }
            })
            .Where(path => IsSupportedImageExtension(Path.GetExtension(path)))
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists && file.LastWriteTimeUtc >= minWriteTime)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .ToList();
    }

    private List<string> GetCandidateScreenshotPaths(string plan)
    {
        if (string.IsNullOrWhiteSpace(plan))
            return [];

        var files = new List<string>();

        // Preferred machine-readable marker from prompt response.
        var markerPattern = @"SCREENSHOT_PATHS?\s*:\s*(.+)";
        var markerMatch = Regex.Match(plan, markerPattern, RegexOptions.IgnoreCase);
        if (markerMatch.Success)
        {
            var payload = markerMatch.Groups[1].Value;
            var tokens = payload
                .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                var normalized = token.Trim('"', '\'', '`', '*', '.', ',', ';', ')', ']', '}');
                if (string.IsNullOrWhiteSpace(normalized))
                    continue;

                var resolved = ResolvePath(normalized);
                if (!string.IsNullOrWhiteSpace(resolved))
                    files.Add(resolved);
            }
        }

        // Fallback parser: detect Windows/UNC file paths even when folders contain spaces.
        var pathPattern = @"(?:[A-Za-z]:\\|\\\\)[^\r\n]*?\.(?:png|jpg|jpeg|webp|gif|bmp)";
        var matches = Regex.Matches(plan, pathPattern, RegexOptions.IgnoreCase);
        foreach (Match match in matches)
        {
            var raw = match.Value.Trim();
            var normalized = raw.Trim('"', '\'', '`', '*', '.', ',', ';', ')', ']', '}');
            var resolved = ResolvePath(normalized);
            if (!string.IsNullOrWhiteSpace(resolved))
                files.Add(resolved);
        }

        return files;
    }

    private static IEnumerable<string> GetAutoOpenClawScreenshotFolders()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
            return [];

        var openClawRoot = Path.Combine(userProfile, ".openclaw");
        if (!Directory.Exists(openClawRoot))
            return [];

        try
        {
            return Directory
                .EnumerateDirectories(openClawRoot, "Fix Screenshot", SearchOption.AllDirectories)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (Path.IsPathRooted(expanded))
            return expanded;

        try
        {
            return Path.GetFullPath(Path.Combine(_environment.ContentRootPath, expanded));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsSupportedImageExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractPullRequestUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // First preference: GitHub pull request links.
        var githubPrMatch = Regex.Match(
            text,
            @"https?://github\.com/[^\s`""'<>]+?/pull/\d+",
            RegexOptions.IgnoreCase);
        if (githubPrMatch.Success)
            return githubPrMatch.Value.Trim().TrimEnd('.', ',', ';', ')', ']');

        // Fallback: any URL containing `/pull/`.
        var anyPrMatch = Regex.Match(
            text,
            @"https?://[^\s`""'<>]+?/pull/[^\s`""'<>]+",
            RegexOptions.IgnoreCase);
        if (anyPrMatch.Success)
            return anyPrMatch.Value.Trim().TrimEnd('.', ',', ';', ')', ']');

        return null;
    }
}
