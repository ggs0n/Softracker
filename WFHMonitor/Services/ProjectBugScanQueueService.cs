using System.Threading.Channels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public sealed class ProjectBugScanQueueService : BackgroundService, IProjectBugScanQueueService
{
    private readonly Channel<ProjectBugScanQueueItem> _queue = Channel.CreateBounded<ProjectBugScanQueueItem>(
        new BoundedChannelOptions(50)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectBugScanQueueService> _logger;
    private readonly CodexSettings _codexSettings;
    private readonly IWebHostEnvironment _environment;

    public ProjectBugScanQueueService(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectBugScanQueueService> logger,
        IOptions<CodexSettings> codexSettings,
        IWebHostEnvironment environment)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _codexSettings = codexSettings.Value ?? new CodexSettings();
        _environment = environment;
    }

    public Task EnqueueAsync(ProjectBugScanQueueItem item, CancellationToken cancellationToken = default)
    {
        if (item.ProjectId <= 0 || string.IsNullOrWhiteSpace(item.ScanAgentId) || string.IsNullOrWhiteSpace(item.RequestedByUserId))
            throw new ArgumentException("Invalid project bug scan queue item.");

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
                _logger.LogError(ex, "Project bug scan queue failed for project {ProjectId}.", item.ProjectId);
                await MarkProjectFailedAsync(item.ProjectId, "Unexpected scan failure. Check server logs.");
            }
        }
    }

    private async Task ProcessItemAsync(ProjectBugScanQueueItem item, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var codex = scope.ServiceProvider.GetRequiredService<ICodexBugScanService>();
        var bugService = scope.ServiceProvider.GetRequiredService<IBugService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var systemSettings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();

        var project = await db.ChangeRequests
            .Include(c => c.Features)
            .Include(c => c.RepositoryFeatures)
            .FirstOrDefaultAsync(c => c.Id == item.ProjectId, cancellationToken);
        if (project == null)
            return;

        var requester = await userManager.FindByIdAsync(item.RequestedByUserId);
        if (requester == null)
        {
            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = "Scan requester no longer exists.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var proSettings = await systemSettings.GetProVersionSettingsAsync();
        if (!proSettings.EnableCodexAgents)
        {
            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = "Codex agents are temporarily disabled by admin.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var hasCodexAccess = HasActiveProAccess(requester)
            || proSettings.AllowCodexForFreePlan;
        if (!hasCodexAccess)
        {
            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = "Find Bugs (Codex) is available for Pro plan only.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var configuredScanAgentIds = CodexBugScanService.GetConfiguredAgentIds(_codexSettings);
        var selectedScanAgentId = item.ScanAgentId.Trim();
        if (configuredScanAgentIds.Count > 0 &&
            !configuredScanAgentIds.Any(a => a.Equals(selectedScanAgentId, StringComparison.OrdinalIgnoreCase)))
        {
            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = "Selected Codex scan agent is not allowed by configuration.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var codexAgentUserId = await ResolveCodexAgentUserIdAsync(userManager);
        if (string.IsNullOrWhiteSpace(codexAgentUserId))
        {
            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = "Codex agent user not found. Create an Agent user first.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        project.BugScanStatus = ProjectBugScanStatus.InProgress;
        project.BugScanLastRunAt = DateTime.UtcNow;
        project.BugScanAgentId = selectedScanAgentId;
        project.BugScanLastMessage = $"Scan started with '{selectedScanAgentId}'.";
        await db.SaveChangesAsync(cancellationToken);

        var existingTitles = await db.BugReports
            .Where(b => b.ChangeRequestId == project.Id)
            .Select(b => b.Title)
            .ToListAsync(cancellationToken);
        var knownTitles = existingTitles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scanResult = await codex.ScanProjectAsync(project, selectedScanAgentId, cancellationToken);
        if (!scanResult.Succeeded)
        {
            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = TrimMessage(scanResult.Error);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var findings = scanResult.Findings
            .Where(f => knownTitles.Add(f.Title))
            .Take(codex.MaxFindingsPerScan)
            .ToList();
        var agentResponsePaths = ScreenshotFileHelper.ExtractCandidatePaths(
            scanResult.AgentResponseText,
            _environment.ContentRootPath);
        var fallbackScreenshotSource = GetConfiguredScreenshotFiles();
        var fallbackScreenshotFiles = fallbackScreenshotSource.Files;
        var combinedFallback = new List<string>(agentResponsePaths.Count + fallbackScreenshotFiles.Count);
        combinedFallback.AddRange(agentResponsePaths);
        combinedFallback.AddRange(fallbackScreenshotFiles);
        var consumedScreenshotFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new ScreenshotImportDiagnostics
        {
            SourceFolderCount = fallbackScreenshotSource.SourceFolderCount,
            ExistingFolderCount = fallbackScreenshotSource.ExistingFolderCount,
            FallbackPoolCount = combinedFallback.Count
        };
        var findingsWithExplicitPaths = 0;
        var explicitPathCount = 0;

        if (findings.Count == 0)
        {
            project.BugScanStatus = ProjectBugScanStatus.Completed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = "Scan complete - no new bugs found for this project.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var created = 0;
        var importedScreenshots = 0;
        var errors = new List<string>();
        foreach (var finding in findings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var model = new BugFormViewModel
            {
                Title = finding.Title,
                Description = finding.Description,
                Workflow = finding.Workflow,
                StepsToReproduce = finding.StepsToReproduce,
                ModuleImpacted = finding.ModuleImpacted,
                Severity = BugSeverityResolver.Resolve(
                    finding.Severity,
                    finding.Title,
                    finding.Description,
                    finding.Workflow,
                    finding.StepsToReproduce,
                    finding.ModuleImpacted),
                Status = BugStatus.New,
                AssigneeType = BugAssigneeType.Agent,
                AgentStatus = BugAgentStatus.Queued,
                AssignedAgentId = codexAgentUserId,
                ChangeRequestId = project.Id,
                ChangeRequestReferenceText = project.CrNumber
            };

            var result = await bugService.CreateAsync(model, item.RequestedByUserId);
            if (result.Succeeded)
            {
                created++;
                if (finding.ScreenshotPaths.Count > 0)
                {
                    findingsWithExplicitPaths++;
                    explicitPathCount += finding.ScreenshotPaths.Count;
                }
                var screenshotCandidates =
                    finding.ScreenshotPaths.Count > 0
                        ? finding.ScreenshotPaths
                        : (IReadOnlyList<string>)combinedFallback;
                var importLimitPerBug = finding.ScreenshotPaths.Count > 0
                    ? Math.Clamp(_codexSettings.FeatureScreenshotImportMaxFiles, 1, 20)
                    : 1;

                var importResult = await TryImportBugScreenshotsAsync(
                    db,
                    result.BugId,
                    screenshotCandidates,
                    consumedScreenshotFiles,
                    importLimitPerBug,
                    cancellationToken);
                importedScreenshots += importResult.Imported;
                diagnostics.Imported += importResult.Imported;
                diagnostics.Candidates += importResult.Candidates;
                diagnostics.InvalidPath += importResult.InvalidPath;
                diagnostics.ConsumedAlready += importResult.ConsumedAlready;
                diagnostics.MissingFile += importResult.MissingFile;
                diagnostics.DuplicateName += importResult.DuplicateName;
                diagnostics.NonImage += importResult.NonImage;
                diagnostics.CopyError += importResult.CopyError;
            }
            else if (!string.IsNullOrWhiteSpace(result.Error))
                errors.Add(result.Error);
        }

        project.BugScanLastRunAt = DateTime.UtcNow;
        if (created == 0 && errors.Count > 0)
        {
            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastMessage = TrimMessage(errors[0]);
        }
        else
        {
            project.BugScanStatus = ProjectBugScanStatus.Completed;
            var summary = created > 0
                ? $"Scan complete with '{selectedScanAgentId}'. Added {created} bug(s)."
                : "Scan complete - no new bugs found for this project.";
            if (importedScreenshots > 0)
                summary = $"{summary} Imported {importedScreenshots} screenshot(s).";
            else if (created > 0)
                summary = $"{summary} No screenshots imported.";
            summary = $"{summary} {BuildScreenshotDebugSummary(findingsWithExplicitPaths, explicitPathCount, diagnostics)}";
            if (errors.Count > 0)
                summary = $"{summary} {errors.Count} item(s) could not be created.";
            project.BugScanLastMessage = TrimMessage(summary);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ScreenshotImportResult> TryImportBugScreenshotsAsync(
        ApplicationDbContext db,
        int bugId,
        IReadOnlyList<string> screenshotPaths,
        ISet<string> consumedSourceFiles,
        int maxFilesPerBug,
        CancellationToken cancellationToken)
    {
        var result = new ScreenshotImportResult
        {
            Candidates = screenshotPaths.Count
        };

        if (bugId <= 0 || screenshotPaths.Count == 0 || maxFilesPerBug <= 0)
            return result;

        var existingOriginalNames = await db.BugScreenshots
            .AsNoTracking()
            .Where(s => s.BugReportId == bugId)
            .Select(s => s.OriginalFileName)
            .ToListAsync(cancellationToken);

        var existingSet = existingOriginalNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "bugs", "screenshots");
        Directory.CreateDirectory(uploadFolder);

        var imported = 0;
        var seenSourceFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pathCandidate in screenshotPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (imported >= maxFilesPerBug)
                break;

            var resolvedPath = ScreenshotFileHelper.ResolvePath(pathCandidate, _environment.ContentRootPath);
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                result.InvalidPath++;
                continue;
            }

            if (!seenSourceFiles.Add(resolvedPath))
            {
                result.InvalidPath++;
                continue;
            }
            if (consumedSourceFiles.Contains(resolvedPath))
            {
                result.ConsumedAlready++;
                continue;
            }

            if (!File.Exists(resolvedPath))
            {
                result.MissingFile++;
                continue;
            }

            var originalName = Path.GetFileName(resolvedPath);
            if (string.IsNullOrWhiteSpace(originalName) || existingSet.Contains(originalName))
            {
                result.DuplicateName++;
                continue;
            }

            var extension = Path.GetExtension(originalName);
            if (!ScreenshotFileHelper.IsSupportedImageExtension(extension))
            {
                result.NonImage++;
                continue;
            }

            try
            {
                var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
                var destination = Path.Combine(uploadFolder, storedName);
                File.Copy(resolvedPath, destination, overwrite: false);

                db.BugScreenshots.Add(new BugScreenshot
                {
                    BugReportId = bugId,
                    FileName = storedName,
                    OriginalFileName = originalName
                });

                existingSet.Add(originalName);
                consumedSourceFiles.Add(resolvedPath);
                imported++;
            }
            catch (Exception ex)
            {
                result.CopyError++;
                _logger.LogWarning(
                    ex,
                    "Failed to import bug screenshot '{Path}' for bug {BugId}.",
                    resolvedPath,
                    bugId);
            }
        }

        result.Imported = imported;
        return result;
    }

    private async Task MarkProjectFailedAsync(int projectId, string message)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = await db.ChangeRequests.FirstOrDefaultAsync(c => c.Id == projectId);
            if (project == null)
                return;

            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = TrimMessage(message);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to persist failed project scan status for project {ProjectId}.", projectId);
        }
    }

    private static async Task<string?> ResolveCodexAgentUserIdAsync(UserManager<ApplicationUser> userManager)
    {
        var agents = await userManager.GetUsersInRoleAsync("Agent");
        var preferred = agents
            .OrderBy(a => a.FullName)
            .FirstOrDefault(a =>
                (a.FullName?.Contains("codex", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.UserName?.Contains("codex", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.Email?.Contains("codex", StringComparison.OrdinalIgnoreCase) ?? false));

        return preferred?.Id ?? agents.OrderBy(a => a.FullName).FirstOrDefault()?.Id;
    }

    private static string TrimMessage(string value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length <= 500)
            return text;
        return text[..500].Trim();
    }

    private ScreenshotSourceFiles GetConfiguredScreenshotFiles()
    {
        var folders = new List<string>();

        if (!string.IsNullOrWhiteSpace(_codexSettings.FeatureScreenshotImportDirectory))
            folders.Add(_codexSettings.FeatureScreenshotImportDirectory.Trim());

        if (_codexSettings.FeatureScreenshotImportDirectories is not null)
        {
            folders.AddRange(_codexSettings.FeatureScreenshotImportDirectories
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path.Trim()));
        }

        folders.AddRange(ScreenshotFileHelper.GetAutoCodexScreenshotFolders());

        var uniqueFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolvedFolders = new List<string>();
        foreach (var folder in folders)
        {
            var resolved = ScreenshotFileHelper.ResolvePath(folder, _environment.ContentRootPath);
            if (!string.IsNullOrWhiteSpace(resolved) && uniqueFolders.Add(resolved))
                resolvedFolders.Add(resolved);
        }

        if (resolvedFolders.Count == 0)
        {
            return new ScreenshotSourceFiles
            {
                Files = [],
                SourceFolderCount = uniqueFolders.Count,
                ExistingFolderCount = 0
            };
        }

        var lookbackMinutes = Math.Clamp(_codexSettings.FeatureScreenshotImportLookbackMinutes, 1, 24 * 60);
        var minWriteTime = DateTime.UtcNow.AddMinutes(-lookbackMinutes);
        var existingFolders = resolvedFolders.Where(Directory.Exists).ToList();
        var files = existingFolders
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
            .Where(path => ScreenshotFileHelper.IsSupportedImageExtension(Path.GetExtension(path)))
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists && file.LastWriteTimeUtc >= minWriteTime)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .ToList();

        return new ScreenshotSourceFiles
        {
            Files = files,
            SourceFolderCount = uniqueFolders.Count,
            ExistingFolderCount = existingFolders.Count
        };
    }

    private static string BuildScreenshotDebugSummary(
        int findingsWithExplicitPaths,
        int explicitPathCount,
        ScreenshotImportDiagnostics diagnostics)
    {
        return $"[shotdbg fldr:{diagnostics.ExistingFolderCount}/{diagnostics.SourceFolderCount} pool:{diagnostics.FallbackPoolCount} exp:{findingsWithExplicitPaths}/{explicitPathCount} cand:{diagnostics.Candidates} imp:{diagnostics.Imported} miss:{diagnostics.MissingFile} dup:{diagnostics.DuplicateName + diagnostics.ConsumedAlready} inv:{diagnostics.InvalidPath} nonimg:{diagnostics.NonImage} err:{diagnostics.CopyError}]";
    }

    private sealed class ScreenshotSourceFiles
    {
        public List<string> Files { get; init; } = [];
        public int SourceFolderCount { get; init; }
        public int ExistingFolderCount { get; init; }
    }

    private sealed class ScreenshotImportResult
    {
        public int Candidates { get; set; }
        public int Imported { get; set; }
        public int InvalidPath { get; set; }
        public int ConsumedAlready { get; set; }
        public int MissingFile { get; set; }
        public int DuplicateName { get; set; }
        public int NonImage { get; set; }
        public int CopyError { get; set; }
    }

    private sealed class ScreenshotImportDiagnostics
    {
        public int SourceFolderCount { get; set; }
        public int ExistingFolderCount { get; set; }
        public int FallbackPoolCount { get; set; }
        public int Candidates { get; set; }
        public int Imported { get; set; }
        public int InvalidPath { get; set; }
        public int ConsumedAlready { get; set; }
        public int MissingFile { get; set; }
        public int DuplicateName { get; set; }
        public int NonImage { get; set; }
        public int CopyError { get; set; }
    }

    private static bool HasActiveProAccess(ApplicationUser user)
    {
        if (user.SubscriptionPlan != SubscriptionPlan.Pro || !user.IsProSubscriptionActive)
            return false;

        return !user.ProSubscriptionEndsAt.HasValue || user.ProSubscriptionEndsAt.Value > DateTime.UtcNow;
    }
}
