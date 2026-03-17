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
    private readonly Channel<ProjectBugScanQueueItem> _queue = Channel.CreateUnbounded<ProjectBugScanQueueItem>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectBugScanQueueService> _logger;
    private readonly OpenClawSettings _openClawSettings;

    public ProjectBugScanQueueService(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectBugScanQueueService> logger,
        IOptions<OpenClawSettings> openClawSettings)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _openClawSettings = openClawSettings.Value ?? new OpenClawSettings();
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
        var openClaw = scope.ServiceProvider.GetRequiredService<IOpenClawBugScanService>();
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
        if (!proSettings.EnableOpenClawAgents)
        {
            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = "OpenClaw agents are temporarily disabled by admin.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var hasOpenClawAccess = HasActiveProAccess(requester)
            || proSettings.AllowOpenClawForFreePlan;
        if (!hasOpenClawAccess)
        {
            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = "Find Bugs (OpenClaw) is available for Pro plan only.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var configuredScanAgentIds = OpenClawBugScanService.GetConfiguredAgentIds(_openClawSettings);
        var selectedScanAgentId = item.ScanAgentId.Trim();
        if (configuredScanAgentIds.Count > 0 &&
            !configuredScanAgentIds.Any(a => a.Equals(selectedScanAgentId, StringComparison.OrdinalIgnoreCase)))
        {
            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = "Selected OpenClaw scan agent is not allowed by configuration.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var openClawAgentUserId = await ResolveOpenClawAgentUserIdAsync(userManager);
        if (string.IsNullOrWhiteSpace(openClawAgentUserId))
        {
            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = "OpenClaw agent user not found. Create an Agent user first.";
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

        var scanResult = await openClaw.ScanProjectAsync(project, selectedScanAgentId, cancellationToken);
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
            .Take(openClaw.MaxFindingsPerScan)
            .ToList();

        if (findings.Count == 0)
        {
            project.BugScanStatus = ProjectBugScanStatus.Completed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = "Scan complete - no new bugs found for this project.";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var created = 0;
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
                Status = BugStatus.New,
                AssigneeType = BugAssigneeType.Agent,
                AgentStatus = BugAgentStatus.Queued,
                AssignedAgentId = openClawAgentUserId,
                ChangeRequestId = project.Id,
                ChangeRequestReferenceText = project.CrNumber
            };

            var result = await bugService.CreateAsync(model, item.RequestedByUserId);
            if (result.Succeeded)
                created++;
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
            if (errors.Count > 0)
                summary = $"{summary} {errors.Count} item(s) could not be created.";
            project.BugScanLastMessage = TrimMessage(summary);
        }

        await db.SaveChangesAsync(cancellationToken);
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

    private static async Task<string?> ResolveOpenClawAgentUserIdAsync(UserManager<ApplicationUser> userManager)
    {
        var agents = await userManager.GetUsersInRoleAsync("Agent");
        var preferred = agents
            .OrderBy(a => a.FullName)
            .FirstOrDefault(a =>
                (a.FullName?.Contains("openclaw", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.UserName?.Contains("openclaw", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.Email?.Contains("openclaw", StringComparison.OrdinalIgnoreCase) ?? false));

        return preferred?.Id ?? agents.OrderBy(a => a.FullName).FirstOrDefault()?.Id;
    }

    private static string TrimMessage(string value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length <= 500)
            return text;
        return text[..500].Trim();
    }

    private static bool HasActiveProAccess(ApplicationUser user)
    {
        if (user.SubscriptionPlan != SubscriptionPlan.Pro || !user.IsProSubscriptionActive)
            return false;

        return !user.ProSubscriptionEndsAt.HasValue || user.ProSubscriptionEndsAt.Value > DateTime.UtcNow;
    }
}
