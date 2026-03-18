using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin,Tester,Developer,Agent")]
public class AgentController : Controller
{
    private static readonly string[] EmployeeRoles = ["Employee", "Developer", "Tester"];
    private static readonly string[] AgentPalette =
    [
        "#22d3ee",
        "#34d399",
        "#f59e0b",
        "#60a5fa",
        "#f472b6",
        "#a78bfa",
        "#fb7185",
        "#4ade80"
    ];
    private static readonly string[] EmployeePalette =
    [
        "#93c5fd",
        "#86efac",
        "#fde68a",
        "#f9a8d4",
        "#c4b5fd",
        "#5eead4"
    ];

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AgentController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var isAdmin = User.IsInRole("Admin");
        var currentUserId = currentUser?.Id ?? string.Empty;
        var currentCompanyName = NormalizeCompanyName(currentUser?.CompanyName);
        var viewerTeamId = currentUser?.OrgTeamId;

        var orgTeams = await ApplyTeamCompanyScope(_db.OrgTeams, currentCompanyName)
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.Name);

        var profile = await _db.OrganizationProfiles
            .Include(p => p.CeoUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == 1);

        var agentUsers = (await _userManager.GetUsersInRoleAsync("Agent"))
            .Where(u => IsCompanyVisibleToViewer(u.CompanyName, currentCompanyName, currentUserId, u.Id))
            .Where(u => IsVisibleToViewer(u.OrgTeamId, isAdmin, viewerTeamId))
            .OrderBy(u => u.FullName)
            .ToList();

        var employeeRoleMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var employeesById = new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in EmployeeRoles)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);
            foreach (var user in users)
            {
                if (!IsCompanyVisibleToViewer(user.CompanyName, currentCompanyName, currentUserId, user.Id))
                    continue;

                if (!IsVisibleToViewer(user.OrgTeamId, isAdmin, viewerTeamId))
                    continue;

                if (!employeesById.ContainsKey(user.Id))
                    employeesById[user.Id] = user;
                if (!employeeRoleMap.ContainsKey(user.Id))
                    employeeRoleMap[user.Id] = role;
            }
        }

        var visibleAgentIds = agentUsers
            .Select(a => a.Id)
            .ToList();

        var visibleProjectsQuery = _db.ChangeRequests
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(currentCompanyName))
            visibleProjectsQuery = visibleProjectsQuery.Where(c => c.CreatedBy != null && c.CreatedBy.CompanyName == currentCompanyName);
        else if (!string.IsNullOrWhiteSpace(currentUserId))
            visibleProjectsQuery = visibleProjectsQuery.Where(c => c.CreatedById == currentUserId);

        var visibleProjectIds = await visibleProjectsQuery
            .Select(c => c.Id)
            .ToListAsync();

        var activeBugStatuses = new[] { BugAgentStatus.Queued, BugAgentStatus.InProgress, BugAgentStatus.Blocked };
        var activeBugAssignments = await _db.BugReports
            .AsNoTracking()
            .Where(b =>
                b.ChangeRequestId.HasValue &&
                visibleProjectIds.Contains(b.ChangeRequestId.Value) &&
                b.AssigneeType == BugAssigneeType.Agent &&
                b.AssignedDeveloperId != null &&
                visibleAgentIds.Contains(b.AssignedDeveloperId) &&
                activeBugStatuses.Contains(b.AgentStatus))
            .Select(b => new AgentTaskAssignment
            {
                AgentId = b.AssignedDeveloperId!,
                WorkType = "Bug",
                WorkNumber = b.BugNumber,
                WorkTitle = b.Title,
                UpdatedAt = b.UpdatedAt
            })
            .ToListAsync();

        var activeFeatureAssignments = await _db.ProjectFeatures
            .AsNoTracking()
            .Where(f =>
                visibleProjectIds.Contains(f.ChangeRequestId) &&
                f.AssignedDeveloperId != null &&
                visibleAgentIds.Contains(f.AssignedDeveloperId) &&
                f.Status != CrStatus.Done &&
                f.AgentStatus != FeatureAgentStatus.PrRaised)
            .Select(f => new AgentTaskAssignment
            {
                AgentId = f.AssignedDeveloperId!,
                WorkType = "Feature",
                WorkNumber = f.FeatureNumber,
                WorkTitle = f.Name,
                UpdatedAt = f.AgentLastRunAt ?? f.CreatedAt
            })
            .ToListAsync();

        var activeAssignments = activeBugAssignments
            .Concat(activeFeatureAssignments)
            .ToList();

        var assignmentLookup = activeAssignments
            .GroupBy(a => a.AgentId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.UpdatedAt).ToList(),
                StringComparer.OrdinalIgnoreCase);

        var onlineThreshold = DateTime.UtcNow.AddMinutes(-5);
        var agents = agentUsers
            .Select((agent, index) =>
            {
                var hasAssignments = assignmentLookup.TryGetValue(agent.Id, out var list);
                var activeCount = hasAssignments ? list!.Count : 0;
                var latest = hasAssignments ? list![0] : null;

                var deskColumn = index % 4;
                var deskRow = index / 4;
                var workLeft = 14 + (deskColumn * 20);
                var workTop = 18 + (deskRow * 18);
                var idleLeft = 24 + ((index % 6) * 10);
                var idleTop = 70 + ((index / 6) * 6);

                return new AgentOfficeAvatarViewModel
                {
                    UserId = agent.Id,
                    Name = string.IsNullOrWhiteSpace(agent.FullName) ? (agent.UserName ?? "Agent") : agent.FullName,
                    Email = agent.Email ?? "-",
                    TeamName = ResolveTeamName(agent.OrgTeamId, orgTeams),
                    WorkspaceName = $"Desk {index + 1:00}",
                    IsWorking = activeCount > 0,
                    IsOnline = agent.LastActivityAt.HasValue && agent.LastActivityAt.Value >= onlineThreshold,
                    LastActivityAt = agent.LastActivityAt,
                    ActiveTaskCount = activeCount,
                    TaskSummary = latest == null
                        ? "Idle - waiting for assignment"
                        : $"{latest.WorkType}: {latest.WorkNumber} - {TrimSummary(latest.WorkTitle, 36)}",
                    AccentColor = AgentPalette[index % AgentPalette.Length],
                    IdleLeftPct = idleLeft,
                    IdleTopPct = idleTop,
                    WorkLeftPct = workLeft,
                    WorkTopPct = workTop
                };
            })
            .ToList();

        var employees = employeesById.Values
            .OrderBy(e => e.FullName)
            .Select((employee, index) =>
            {
                var row = index / 8;
                var col = index % 8;
                return new OfficeEmployeeAvatarViewModel
                {
                    UserId = employee.Id,
                    Name = string.IsNullOrWhiteSpace(employee.FullName) ? (employee.UserName ?? "Employee") : employee.FullName,
                    Role = employeeRoleMap.TryGetValue(employee.Id, out var role) ? role : "Employee",
                    TeamName = ResolveTeamName(employee.OrgTeamId, orgTeams),
                    IsOnline = employee.LastActivityAt.HasValue && employee.LastActivityAt.Value >= onlineThreshold,
                    LastActivityAt = employee.LastActivityAt,
                    AccentColor = EmployeePalette[index % EmployeePalette.Length],
                    LeftPct = 8 + (col * 10),
                    TopPct = 84 + (row * 5)
                };
            })
            .ToList();

        var visibleTeamCount = orgTeams.Count;
        var visibleProfileCeo = profile?.CeoUser != null
            && IsCompanyVisibleToViewer(profile.CeoUser.CompanyName, currentCompanyName, currentUserId, profile.CeoUser.Id);
        var vm = new AgentDashboardViewModel
        {
            CeoName = visibleProfileCeo
                ? (profile!.CeoUser!.FullName ?? (string.IsNullOrWhiteSpace(currentUser?.FullName) ? "CEO" : currentUser!.FullName))
                : (string.IsNullOrWhiteSpace(currentUser?.FullName) ? "CEO" : currentUser!.FullName),
            CeoEmail = visibleProfileCeo
                ? (profile!.CeoUser!.Email ?? currentUser?.Email ?? string.Empty)
                : (currentUser?.Email ?? string.Empty),
            TeamCount = visibleTeamCount,
            AgentCount = agents.Count,
            EmployeeCount = employees.Count,
            WorkingAgentCount = agents.Count(a => a.IsWorking),
            ActiveTaskCount = agents.Sum(a => a.ActiveTaskCount),
            GeneratedAtUtc = DateTime.UtcNow,
            Agents = agents,
            Employees = employees
        };

        ViewData["Title"] = "Agent Office";
        return View(vm);
    }

    private static bool IsVisibleToViewer(int? entityTeamId, bool isAdmin, int? viewerTeamId)
    {
        if (isAdmin)
            return true;

        if (!viewerTeamId.HasValue)
            return !entityTeamId.HasValue;

        return !entityTeamId.HasValue || entityTeamId.Value == viewerTeamId.Value;
    }

    private static string ResolveTeamName(int? teamId, IReadOnlyDictionary<int, string> teamLookup)
    {
        if (teamId.HasValue && teamLookup.TryGetValue(teamId.Value, out var name))
            return name;

        return "Unassigned";
    }

    private static string TrimSummary(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var clean = value.Trim();
        return clean.Length <= maxLength ? clean : $"{clean[..maxLength].Trim()}...";
    }

    private static IQueryable<OrgTeam> ApplyTeamCompanyScope(IQueryable<OrgTeam> query, string? companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            return query.Where(t => t.CompanyName == null || t.CompanyName == string.Empty);

        return query.Where(t => t.CompanyName == companyName);
    }

    private static string? NormalizeCompanyName(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool IsCompanyVisibleToViewer(string? targetCompanyName, string? viewerCompanyName, string? viewerUserId, string? targetUserId)
    {
        if (!string.IsNullOrWhiteSpace(viewerCompanyName))
            return string.Equals(NormalizeCompanyName(targetCompanyName), viewerCompanyName, StringComparison.OrdinalIgnoreCase);

        return !string.IsNullOrWhiteSpace(viewerUserId) &&
               !string.IsNullOrWhiteSpace(targetUserId) &&
               string.Equals(viewerUserId, targetUserId, StringComparison.Ordinal);
    }

    private sealed class AgentTaskAssignment
    {
        public string AgentId { get; init; } = string.Empty;
        public string WorkType { get; init; } = string.Empty;
        public string WorkNumber { get; init; } = string.Empty;
        public string WorkTitle { get; init; } = string.Empty;
        public DateTime UpdatedAt { get; init; }
    }
}
