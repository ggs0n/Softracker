using System.Threading.Channels;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public sealed class BugFixQueueService : BackgroundService, IBugFixQueueService
{
    private readonly Channel<BugFixQueueItem> _queue = Channel.CreateUnbounded<BugFixQueueItem>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
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
        var openClaw = scope.ServiceProvider.GetRequiredService<IOpenClawBugScanService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var systemSettings = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();

        var bug = await db.BugReports
            .Include(b => b.ChangeRequest)
            .FirstOrDefaultAsync(b => b.Id == item.BugId, cancellationToken);
        if (bug == null)
            return;

        var settings = await systemSettings.GetProVersionSettingsAsync();
        if (!settings.EnableOpenClawAgents)
        {
            bug.AgentStatus = BugAgentStatus.Failed;
            bug.UpdatedAt = DateTime.UtcNow;
            db.BugActivities.Add(new BugActivity
            {
                BugReportId = bug.Id,
                Action = TrimActivityText("OpenClaw fix aborted: OpenClaw agents are disabled by admin."),
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
            Action = TrimActivityText($"OpenClaw fix started ({item.FixAgentId})"),
            OldStatus = bug.Status,
            NewStatus = bug.Status,
            OldAssignedDeveloperId = oldAssignedId,
            NewAssignedDeveloperId = bug.AssignedDeveloperId
        });
        await db.SaveChangesAsync(cancellationToken);

        var fixResult = await openClaw.FixBugAsync(bug, item.FixAgentId, cancellationToken);
        if (!fixResult.Succeeded)
        {
            bug.AgentStatus = BugAgentStatus.Failed;
            bug.UpdatedAt = DateTime.UtcNow;
            db.BugActivities.Add(new BugActivity
            {
                BugReportId = bug.Id,
                Action = TrimActivityText($"OpenClaw fix failed: {fixResult.Error}"),
                OldStatus = bug.Status,
                NewStatus = bug.Status,
                OldAssignedDeveloperId = bug.AssignedDeveloperId,
                NewAssignedDeveloperId = bug.AssignedDeveloperId
            });
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var generatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'");
        var fixBlock = $"[OpenClaw Fix Plan - {item.FixAgentId} - {generatedAt}]\n{fixResult.FixPlan.Trim()}";
        bug.Workflow = AppendTextWithLimit(bug.Workflow, fixBlock, 4000);
        var resolvedPullRequestUrl = ResolvePullRequestUrl(fixResult, bug.ChangeRequest?.GitHubRepoUrl);
        if (!string.IsNullOrWhiteSpace(resolvedPullRequestUrl))
            bug.PullRequestUrl = resolvedPullRequestUrl;

        bug.AgentStatus = BugAgentStatus.PrRaised;
        bug.UpdatedAt = DateTime.UtcNow;

        db.BugActivities.Add(new BugActivity
        {
            BugReportId = bug.Id,
            Action = TrimActivityText(
                string.IsNullOrWhiteSpace(resolvedPullRequestUrl)
                    ? $"OpenClaw fix completed ({item.FixAgentId})"
                    : $"OpenClaw fix completed ({item.FixAgentId}) with PR link"),
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
            item.FixAgentId,
            cancellationToken);
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

    private static string? ResolvePullRequestUrl(OpenClawBugFixResult fixResult, string? repositoryUrl)
    {
        var direct = NormalizePullRequestUrl(fixResult.PullRequestUrl);
        if (!string.IsNullOrWhiteSpace(direct))
            return direct;

        var prNumber = ExtractPullRequestNumber(fixResult.FixPlan);
        var repoBase = NormalizeRepositoryUrl(repositoryUrl);
        if (!prNumber.HasValue || string.IsNullOrWhiteSpace(repoBase))
            return null;

        return NormalizePullRequestUrl($"{repoBase}/pull/{prNumber.Value}");
    }

    private static string? NormalizePullRequestUrl(string? value)
    {
        var trimmed = (value ?? string.Empty)
            .Trim()
            .TrimEnd('.', ',', ';', ':', ')', ']', '}');

        if (string.IsNullOrWhiteSpace(trimmed) ||
            trimmed.Length > 500 ||
            !Uri.TryCreate(trimmed, UriKind.Absolute, out _))
        {
            return null;
        }

        return trimmed;
    }

    private static int? ExtractPullRequestNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var match = Regex.Match(text, @"\b(?:pr|pull\s*request)\s*#?\s*(\d+)\b", RegexOptions.IgnoreCase);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var number))
            return null;

        return number;
    }

    private static string? NormalizeRepositoryUrl(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^4];

        trimmed = trimmed.TrimEnd('/');
        return Uri.TryCreate(trimmed, UriKind.Absolute, out _) ? trimmed : null;
    }

    private async Task NotifyAdminsPrRaisedAsync(
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        BugReport bug,
        string fixAgentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var admins = await userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count == 0)
                return;

            var title = $"Agent PR raised: {bug.BugNumber}";
            var message = $"{bug.BugNumber} has moved to PRRaised by agent '{fixAgentId}'.";
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
