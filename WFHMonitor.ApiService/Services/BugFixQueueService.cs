using System.Threading.Channels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public sealed class BugFixQueueService : BackgroundService, IBugFixQueueService
{
    private readonly Channel<BugFixQueueItem> _queue = Channel.CreateBounded<BugFixQueueItem>(
        new BoundedChannelOptions(50)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BugFixQueueService> _logger;

    public BugFixQueueService(
        IServiceScopeFactory scopeFactory,
        ILogger<BugFixQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task EnqueueAsync(BugFixQueueItem item, CancellationToken cancellationToken = default)
    {
        if (item.BugId <= 0 || string.IsNullOrWhiteSpace(item.FixAgentId) || string.IsNullOrWhiteSpace(item.AssignedAgentUserId))
            throw new ArgumentException("Invalid bug fix queue item.");

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
                _logger.LogError(ex, "Bug fix queue failed for bug {BugId}.", item.BugId);
            }
        }
    }

    private async Task ProcessItemAsync(BugFixQueueItem item, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var automation =
            scope.ServiceProvider.GetRequiredService<IAiAutomationService>();
        var workspaceService =
            scope.ServiceProvider.GetRequiredService<IRepositoryWorkspaceService>();
        var gitHub =
            scope.ServiceProvider.GetRequiredService<IGitHubService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var systemSettings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();

        var bug = await db.BugReports
            .Include(b => b.ChangeRequest)
            .FirstOrDefaultAsync(b => b.Id == item.BugId, cancellationToken);
        if (bug == null)
            return;

        var settings = await systemSettings.GetProVersionSettingsAsync();
        if (!settings.EnableAiAutomation)
        {
            bug.AgentStatus = BugAgentStatus.Failed;
            bug.UpdatedAt = DateTime.UtcNow;
            db.BugActivities.Add(new BugActivity
            {
                BugReportId = bug.Id,
                Action = TrimActivityText("AI automation fix aborted: AI automation is disabled by admin."),
                OldStatus = bug.Status,
                NewStatus = bug.Status,
                OldAssignedDeveloperId = bug.AssignedDeveloperId,
                NewAssignedDeveloperId = bug.AssignedDeveloperId
            });
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var oldAssignedId = bug.AssignedDeveloperId;
        bug.AssigneeType = BugAssigneeType.Agent;
        bug.AssignedDeveloperId = item.AssignedAgentUserId;
        bug.AgentStatus = BugAgentStatus.InProgress;
        bug.UpdatedAt = DateTime.UtcNow;

        db.BugActivities.Add(new BugActivity
        {
            BugReportId = bug.Id,
            Action = TrimActivityText("AI automation fix started (Codex)"),
            OldStatus = bug.Status,
            NewStatus = bug.Status,
            OldAssignedDeveloperId = oldAssignedId,
            NewAssignedDeveloperId = bug.AssignedDeveloperId
        });
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            if (bug.ChangeRequest is null)
                throw new InvalidOperationException(
                    "The bug is not linked to a GitHub-backed project.");

            var workspace = await workspaceService.PrepareAsync(
                bug.ChangeRequest,
                item.RequestedByUserId,
                bug.BugNumber,
                cancellationToken);
            var fixResult = await automation.FixBugAsync(
                bug,
                workspace.RepositoryDirectory,
                cancellationToken);
            if (!fixResult.Succeeded)
                throw new InvalidOperationException(fixResult.Error);

            var pushResult = await workspaceService.CommitAndPushAsync(
                workspace,
                BuildCommitMessage(bug),
                cancellationToken);
            if (!pushResult.HasChanges)
                throw new InvalidOperationException(
                    pushResult.Summary);

            var pullRequest =
                await gitHub.CreateDraftPullRequestAsync(
                    workspace.Owner,
                    workspace.Repository,
                    $"Fix {bug.BugNumber}: {bug.Title}",
                    workspace.WorkBranch,
                    workspace.BaseBranch,
                    BuildPullRequestBody(
                        bug,
                        fixResult.FixPlan,
                        pushResult.Summary),
                    item.RequestedByUserId,
                    cancellationToken);

            var generatedAt =
                DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'");
            var fixBlock =
                $"[AI Automation Fix - Codex - {generatedAt}]\n" +
                fixResult.FixPlan.Trim();
            bug.Workflow = AppendTextWithLimit(
                bug.Workflow,
                fixBlock,
                4000);
            bug.PullRequestUrl = pullRequest.Url;
            bug.AgentStatus = BugAgentStatus.PrRaised;
            bug.UpdatedAt = DateTime.UtcNow;

            db.BugActivities.Add(new BugActivity
            {
                BugReportId = bug.Id,
                Action = TrimActivityText(
                    $"AI automation fix completed with draft PR #{pullRequest.Number}"),
                OldStatus = bug.Status,
                NewStatus = bug.Status,
                OldAssignedDeveloperId = bug.AssignedDeveloperId,
                NewAssignedDeveloperId = bug.AssignedDeveloperId
            });
            await db.SaveChangesAsync(cancellationToken);
            await NotifyAdminsPrRaisedAsync(
                userManager,
                notificationService,
                bug,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            bug.AgentStatus = BugAgentStatus.Failed;
            bug.UpdatedAt = DateTime.UtcNow;
            db.BugActivities.Add(new BugActivity
            {
                BugReportId = bug.Id,
                Action = TrimActivityText(
                    $"AI automation fix failed: {exception.Message}"),
                OldStatus = bug.Status,
                NewStatus = bug.Status,
                OldAssignedDeveloperId = bug.AssignedDeveloperId,
                NewAssignedDeveloperId = bug.AssignedDeveloperId
            });
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                exception,
                "AI automation fix failed for bug {BugId}.",
                bug.Id);
        }
    }

    private static string BuildCommitMessage(BugReport bug)
    {
        var title = $"{bug.BugNumber} {bug.Title}"
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return $"fix: {title[..Math.Min(65, title.Length)]}";
    }

    private static string BuildPullRequestBody(
        BugReport bug,
        string fixPlan,
        string changedFiles)
    {
        var details = (fixPlan ?? string.Empty).Trim();
        details = details[..Math.Min(30_000, details.Length)];
        var files = (changedFiles ?? string.Empty).Trim();
        files = files[..Math.Min(8_000, files.Length)];
        return $"""
            ## Bug
            {bug.BugNumber}: {bug.Title}

            ## Root cause, changes, tests, and risks
            {details}

            ## Changed files
            ```text
            {files}
            ```

            ## Automation policy
            This pull request was created as a draft. WFHMonitor automation never merges it or marks it ready for review.
            """;
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

    private static string TrimActivityText(string value)
    {
        var text = (value ?? string.Empty).Trim();
        return text.Length <= 200 ? text : text[..200].Trim();
    }

    private async Task NotifyAdminsPrRaisedAsync(
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        BugReport bug,
        CancellationToken cancellationToken)
    {
        try
        {
            var admins = await userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count == 0)
                return;

            var title = $"Draft PR raised: {bug.BugNumber}";
            var message =
                $"{bug.BugNumber} has moved to PRRaised by AI automation. The pull request remains a draft.";
            var linkUrl = $"/Bug/Details/{bug.Id}";

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
            _logger.LogWarning(ex, "Unable to create PRRaised admin notifications for bug {BugId}.", bug.Id);
        }
    }
}
