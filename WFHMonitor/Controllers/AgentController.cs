using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin,Tester,Developer,Agent")]
public class AgentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public AgentController(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        var agentItems = await _db.BugReports
            .AsNoTracking()
            .Where(b => b.AssigneeType == BugAssigneeType.Agent)
            .OrderByDescending(b => b.UpdatedAt)
            .Select(b => new AgentWorkItemViewModel
            {
                Id = b.Id,
                BugNumber = b.BugNumber,
                Title = b.Title,
                AgentStatus = b.AgentStatus.ToString(),
                UpdatedAt = b.UpdatedAt
            })
            .ToListAsync();

        var runningAgentItems = agentItems
            .Where(i => string.Equals(i.AgentStatus, nameof(BugAgentStatus.InProgress), StringComparison.Ordinal))
            .ToList();

        var queueSnapshot = new AgentQueueSnapshotViewModel
        {
            Total = agentItems.Count,
            Queued = agentItems.Count(i => string.Equals(i.AgentStatus, nameof(BugAgentStatus.Queued), StringComparison.Ordinal)),
            InProgress = agentItems.Count(i => string.Equals(i.AgentStatus, nameof(BugAgentStatus.InProgress), StringComparison.Ordinal)),
            Blocked = agentItems.Count(i => string.Equals(i.AgentStatus, nameof(BugAgentStatus.Blocked), StringComparison.Ordinal)),
            PrRaised = agentItems.Count(i => string.Equals(i.AgentStatus, nameof(BugAgentStatus.PrRaised), StringComparison.Ordinal)),
            Failed = agentItems.Count(i => string.Equals(i.AgentStatus, nameof(BugAgentStatus.Failed), StringComparison.Ordinal))
        };

        var recentActivities = await _db.BugActivities
            .AsNoTracking()
            .Include(a => a.BugReport)
            .Where(a => a.BugReport != null && a.BugReport.AssigneeType == BugAssigneeType.Agent)
            .OrderByDescending(a => a.CreatedAt)
            .Take(30)
            .Select(a => new AgentActivityItemViewModel
            {
                BugId = a.BugReportId,
                BugNumber = a.BugReport!.BugNumber,
                Title = a.BugReport!.Title,
                Action = a.Action,
                StatusTransition = a.OldStatus.HasValue || a.NewStatus.HasValue
                    ? $"{(a.OldStatus.HasValue ? a.OldStatus.Value.ToString() : "-")} -> {(a.NewStatus.HasValue ? a.NewStatus.Value.ToString() : "-")}"
                    : null,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        var cronJobs = _configuration
            .GetSection("AgentMonitoring:CronJobs")
            .Get<List<CronJobStatusViewModel>>() ?? new List<CronJobStatusViewModel>();

        var runningCronJobs = cronJobs
            .Where(j => j.IsEnabled && j.IsRunning)
            .ToList();

        var agents = LoadOpenClawAgents();
        var mainAgent = agents.FirstOrDefault(a => string.Equals(a.Name, "OpenClaw Main", StringComparison.Ordinal));
        if (mainAgent != null)
        {
            mainAgent.Status = runningAgentItems.Count > 0 ? "Running" : "Idle";
            mainAgent.RunningWorkItems = runningAgentItems.Count;
            mainAgent.QueuedWorkItems = agentItems.Count(i => string.Equals(i.AgentStatus, nameof(BugAgentStatus.Queued), StringComparison.Ordinal));
        }

        var vm = new AgentDashboardViewModel
        {
            Agents = agents,
            RunningAgentWorkItems = runningAgentItems,
            AgentWorkItems = agentItems,
            QueueSnapshot = queueSnapshot,
            RecentActivities = recentActivities,
            RunningCronJobs = runningCronJobs,
            CronJobs = cronJobs
        };

        ViewData["Title"] = "Agent";
        return View(vm);
    }

    private static List<AgentInfoViewModel> LoadOpenClawAgents()
    {
        var result = new List<AgentInfoViewModel>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var openClawAgentsPath = Path.Combine(userProfile, ".openclaw", "agents");

        if (!Directory.Exists(openClawAgentsPath))
        {
            return new List<AgentInfoViewModel>
            {
                new()
                {
                    Name = "OpenClaw Main",
                    Description = "Default OpenClaw agent profile.",
                    Status = "Unknown"
                }
            };
        }

        var folders = Directory.GetDirectories(openClawAgentsPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var folder in folders)
        {
            var name = string.Equals(folder, "main", StringComparison.OrdinalIgnoreCase)
                ? "OpenClaw Main"
                : $"OpenClaw Agent {folder}";

            var description = string.Equals(folder, "main", StringComparison.OrdinalIgnoreCase)
                ? "Default OpenClaw agent profile."
                : $"OpenClaw agent instance from profile folder '{folder}'.";

            result.Add(new AgentInfoViewModel
            {
                Name = name,
                Description = description,
                Status = "Idle",
                RunningWorkItems = 0,
                QueuedWorkItems = 0
            });
        }

        return result;
    }
}
