using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public sealed class CodeReadinessScanQueueService : BackgroundService, ICodeReadinessScanQueueService
{
    private readonly Channel<CodeReadinessScanQueueItem> _queue = Channel.CreateBounded<CodeReadinessScanQueueItem>(
        new BoundedChannelOptions(20)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CodeReadinessScanQueueService> _logger;

    public CodeReadinessScanQueueService(
        IServiceScopeFactory scopeFactory,
        ILogger<CodeReadinessScanQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task EnqueueAsync(CodeReadinessScanQueueItem item, CancellationToken cancellationToken = default)
    {
        if (item.ProjectId <= 0 || string.IsNullOrWhiteSpace(item.RequestedByUserId))
            throw new ArgumentException("Invalid Code Readiness scan request.");

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
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Code Readiness scan failed unexpectedly for project {ProjectId}", item.ProjectId);
                await MarkFailedAsync(item.ProjectId, "Unexpected scan failure. Check the application logs.");
            }
        }
    }

    private async Task ProcessItemAsync(CodeReadinessScanQueueItem item, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var snapshotService = scope.ServiceProvider.GetRequiredService<IRepositorySnapshotService>();
        var codex = scope.ServiceProvider.GetRequiredService<ICodexBugScanService>();

        var project = await db.ChangeRequests
            .Include(change => change.Features)
            .Include(change => change.RepositoryFeatures)
            .FirstOrDefaultAsync(change => change.Id == item.ProjectId, cancellationToken);
        if (project is null)
            return;

        if (!TryResolveRepository(project, out var owner, out var repository, out var branch))
        {
            await MarkFailedAsync(project, db, "Connect a valid GitHub repository before starting a full scan.", cancellationToken);
            return;
        }

        project.CodeReadinessScanStatus = CodeReadinessScanStatus.InProgress;
        project.CodeReadinessScanStartedAt = DateTime.UtcNow;
        project.CodeReadinessScanCompletedAt = null;
        project.CodeReadinessScanAgentId = item.ScanAgentId;
        project.CodeReadinessScanMessage = "Downloading an isolated repository snapshot.";
        await db.SaveChangesAsync(cancellationToken);

        RepositorySnapshot? snapshot = null;
        try
        {
            snapshot = await snapshotService.CreateAsync(
                project.Id,
                owner,
                repository,
                branch,
                item.RequestedByUserId,
                cancellationToken);

            project.CodeReadinessScanCommitSha = snapshot.CommitSha;
            project.CodeReadinessScanMessage = "Codex is reviewing source files in read-only mode.";
            await db.SaveChangesAsync(cancellationToken);

            var result = await codex.ScanCodeReadinessAsync(
                project,
                snapshot.RootPath,
                snapshot.CommitSha,
                item.ScanAgentId,
                cancellationToken);

            if (!result.Succeeded)
            {
                await MarkFailedAsync(project, db, result.Error, cancellationToken);
                return;
            }

            project.CodeReadinessScanStatus = CodeReadinessScanStatus.Completed;
            project.CodeReadinessScanCompletedAt = DateTime.UtcNow;
            project.CodeReadinessScanMessage = $"Source scan complete: {result.Findings.Count} finding(s) from {result.AnalyzedFiles} analyzed file(s).";
            project.CodeReadinessScanResultJson = JsonSerializer.Serialize(result);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Code Readiness scan failed for project {ProjectId}", project.Id);
            await MarkFailedAsync(project, db, ex.Message, cancellationToken);
        }
        finally
        {
            if (snapshot is not null)
            {
                try
                {
                    await snapshotService.DeleteAsync(snapshot);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to delete repository snapshot {SnapshotDirectory}", snapshot.SnapshotDirectory);
                }
            }
        }
    }

    private async Task MarkFailedAsync(int projectId, string message)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var project = await db.ChangeRequests.FirstOrDefaultAsync(change => change.Id == projectId);
            if (project is not null)
                await MarkFailedAsync(project, db, message, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to persist failed Code Readiness state for project {ProjectId}", projectId);
        }
    }

    private static async Task MarkFailedAsync(
        ChangeRequest project,
        ApplicationDbContext db,
        string message,
        CancellationToken cancellationToken)
    {
        project.CodeReadinessScanStatus = CodeReadinessScanStatus.Failed;
        project.CodeReadinessScanCompletedAt = DateTime.UtcNow;
        project.CodeReadinessScanMessage = Trim(message, 500);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool TryResolveRepository(
        ChangeRequest project,
        out string owner,
        out string repository,
        out string branch)
    {
        owner = project.GitHubRepoOwner?.Trim() ?? string.Empty;
        repository = project.GitHubRepoName?.Trim() ?? string.Empty;
        branch = string.IsNullOrWhiteSpace(project.GitHubBranch) ? "main" : project.GitHubBranch.Trim();
        if (!string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repository))
            return true;

        var match = Regex.Match(
            project.GitHubRepoUrl ?? string.Empty,
            @"github\.com[/:](?<owner>[^/\s]+)/(?<repo>[^/\s#?]+?)(?:\.git)?/?$",
            RegexOptions.IgnoreCase);
        if (!match.Success)
            return false;

        owner = match.Groups["owner"].Value;
        repository = match.Groups["repo"].Value;
        return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repository);
    }

    private static string Trim(string? value, int maxLength)
    {
        var normalized = (value ?? "Scan failed.").Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength].Trim();
    }
}
