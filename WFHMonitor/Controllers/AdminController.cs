using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserRegistrationService _userRegistrationService;

    public AdminController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IUserRegistrationService userRegistrationService)
    {
        _db = db;
        _userManager = userManager;
        _userRegistrationService = userRegistrationService;
    }

    public async Task<IActionResult> Index()
    {
        var accessDenied = EnsureAdminAccess();
        if (accessDenied != null) return accessDenied;

        var today = DateTime.UtcNow.Date;

        var teamRoles = new[] { "Employee", "Developer", "Tester", "Agent" };
        var teamMemberIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var agentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var developerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in teamRoles)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);
            foreach (var user in users)
            {
                teamMemberIds.Add(user.Id);
                if (string.Equals(role, "Agent", StringComparison.OrdinalIgnoreCase))
                    agentIds.Add(user.Id);
                if (string.Equals(role, "Developer", StringComparison.OrdinalIgnoreCase))
                    developerIds.Add(user.Id);
            }
        }

        var agentIdList = agentIds.ToList();
        var developerIdList = developerIds.ToList();

        var tasksDoneToday = await _db.WorkTasks
            .Include(t => t.Assignee)
            .Where(t => t.Status == WorkTaskStatus.Done && t.UpdatedAt.Date == today)
            .AsNoTracking()
            .ToListAsync();

        var blockedTasks = await _db.WorkTasks
            .Include(t => t.Assignee)
            .Where(t => t.Status == WorkTaskStatus.Blocked)
            .AsNoTracking()
            .ToListAsync();

        // Project overview with task & bug breakdowns
        var projects = await _db.ChangeRequests
            .AsNoTracking()
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();

        var allTasks = await _db.WorkTasks
            .Where(t => t.ChangeRequestId != null)
            .GroupBy(t => t.ChangeRequestId)
            .Select(g => new
            {
                ChangeRequestId = g.Key,
                Total = g.Count(),
                Done = g.Count(t => t.Status == WorkTaskStatus.Done),
                InProgress = g.Count(t => t.Status == WorkTaskStatus.InProgress),
                Blocked = g.Count(t => t.Status == WorkTaskStatus.Blocked)
            })
            .ToListAsync();

        var allBugs = await _db.BugReports
            .Where(b => b.ChangeRequestId != null)
            .GroupBy(b => b.ChangeRequestId)
            .Select(g => new
            {
                ChangeRequestId = g.Key,
                Total = g.Count(),
                Open = g.Count(b => b.Status != BugStatus.Complete),
                Complete = g.Count(b => b.Status == BugStatus.Complete)
            })
            .ToListAsync();

        var allFeatures = await _db.ProjectFeatures
            .GroupBy(f => f.ChangeRequestId)
            .Select(g => new
            {
                ChangeRequestId = g.Key,
                Total = g.Count(),
                Completed = g.Count(f => f.IsCompleted)
            })
            .ToListAsync();

        var projectItems = projects.Select(p =>
        {
            var taskStats = allTasks.FirstOrDefault(t => t.ChangeRequestId == p.Id);
            var bugStats = allBugs.FirstOrDefault(b => b.ChangeRequestId == p.Id);
            var featureStats = allFeatures.FirstOrDefault(f => f.ChangeRequestId == p.Id);
            return new ProjectOverviewItem
            {
                Id = p.Id,
                CrNumber = p.CrNumber,
                Title = p.Title,
                Status = p.Status,
                Stage = p.Stage,
                Priority = p.Priority,
                TimelineStart = p.TimelineStart,
                TimelineEnd = p.TimelineEnd,
                TotalTasks = taskStats?.Total ?? 0,
                DoneTasks = taskStats?.Done ?? 0,
                InProgressTasks = taskStats?.InProgress ?? 0,
                BlockedTasks = taskStats?.Blocked ?? 0,
                TotalBugs = bugStats?.Total ?? 0,
                OpenBugs = bugStats?.Open ?? 0,
                CompleteBugs = bugStats?.Complete ?? 0,
                TotalFeatures = featureStats?.Total ?? 0,
                CompletedFeatures = featureStats?.Completed ?? 0
            };
        }).ToList();

        // Global bug stats
        var totalBugs = await _db.BugReports.CountAsync();
        var completeBugs = await _db.BugReports.CountAsync(b => b.Status == BugStatus.Complete);

        var agentFeaturesShipped = agentIdList.Count == 0
            ? 0
            : await _db.ProjectFeatures.CountAsync(f =>
                f.AssignedDeveloperId != null &&
                agentIdList.Contains(f.AssignedDeveloperId) &&
                (f.AgentStatus == FeatureAgentStatus.PrRaised || f.Status == CrStatus.Done || f.IsCompleted));

        var agentBugsFound = await _db.BugReports.CountAsync(b => b.AssigneeType == BugAssigneeType.Agent);

        var agentBugsFixed = await _db.BugReports.CountAsync(b =>
            b.AssigneeType == BugAssigneeType.Agent &&
            (b.AgentStatus == BugAgentStatus.PrRaised || b.Status == BugStatus.Complete));

        var developerBugsFixed = developerIdList.Count == 0
            ? 0
            : await _db.BugReports.CountAsync(b =>
                b.AssigneeType == BugAssigneeType.Developer &&
                b.AssignedDeveloperId != null &&
                developerIdList.Contains(b.AssignedDeveloperId) &&
                b.Status == BugStatus.Complete);

        var developerFeaturesDelivered = developerIdList.Count == 0
            ? 0
            : await _db.ProjectFeatures.CountAsync(f =>
                f.AssignedDeveloperId != null &&
                developerIdList.Contains(f.AssignedDeveloperId) &&
                (f.Status == CrStatus.Done || f.IsCompleted));

        var tasksDoneTodayComposite = agentBugsFixed + agentBugsFound + agentFeaturesShipped + (developerBugsFixed + developerFeaturesDelivered);

        var vm = new AdminDashboardViewModel
        {
            Today = today,
            TasksDoneToday = tasksDoneToday,
            BlockedTasks = blockedTasks,
            TotalEmployees = teamMemberIds.Count,
            TasksDoneTodayCount = tasksDoneTodayComposite,
            Projects = projectItems,
            TotalBugs = totalBugs,
            OpenBugs = totalBugs - completeBugs,
            CompleteBugs = completeBugs,
            AgentFeaturesShipped = agentFeaturesShipped,
            AgentBugsFound = agentBugsFound,
            AgentBugsFixed = agentBugsFixed,
            DeveloperDeliveredItems = developerBugsFixed + developerFeaturesDelivered
        };

        return View(vm);
    }

    public async Task<IActionResult> Employees()
    {
        var accessDenied = EnsureAdminAccess();
        if (accessDenied != null) return accessDenied;

        var vm = await BuildEmployeesViewModel();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddEmployee([Bind(Prefix = "NewEmployee")] RegisterViewModel model)
    {
        var accessDenied = EnsureAdminAccess();
        if (accessDenied != null) return accessDenied;

        var registration = await _userRegistrationService.RegisterAsync(model);
        if (registration.Succeeded)
        {
            TempData["Success"] = $"{registration.Role} {registration.FullName} registered successfully.";
            return RedirectToAction(nameof(Employees));
        }

        foreach (var error in registration.Errors)
            ModelState.AddModelError(string.Empty, error);

        return View("Employees", await BuildEmployeesViewModel(model));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteEmployee(string userId)
    {
        var accessDenied = EnsureAdminAccess();
        if (accessDenied != null) return accessDenied;

        if (string.IsNullOrWhiteSpace(userId))
        {
            TempData["Error"] = "Invalid employee id.";
            return RedirectToAction(nameof(Employees));
        }

        var currentUserId = _userManager.GetUserId(User);
        if (string.Equals(currentUserId, userId, StringComparison.Ordinal))
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction(nameof(Employees));
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "Employee not found.";
            return RedirectToAction(nameof(Employees));
        }

        var tasksCreated = await _db.WorkTasks.CountAsync(t => t.CreatedById == userId);
        var projectsCreated = await _db.ChangeRequests.CountAsync(c => c.CreatedById == userId);
        var bugsCreated = await _db.BugReports.CountAsync(b => b.CreatedById == userId);
        var calendarEventsCreated = 0;
        try
        {
            calendarEventsCreated = await _db.CalendarEvents.CountAsync(c => c.CreatedById == userId);
        }
        catch (SqlException ex) when (ex.Message.Contains("Invalid object name 'CalendarEvents'", StringComparison.OrdinalIgnoreCase))
        {
            // Calendar table may not exist yet in older databases that have not applied newer migrations.
            calendarEventsCreated = 0;
        }
        var projectPicAssignments = await _db.ChangeRequestPics.CountAsync(p => p.EmployeeId == userId);
        var bugActivityAssignments = await _db.BugActivities.CountAsync(a =>
            a.OldAssignedDeveloperId == userId || a.NewAssignedDeveloperId == userId);

        if (tasksCreated > 0 || projectsCreated > 0 || bugsCreated > 0 ||
            calendarEventsCreated > 0 || projectPicAssignments > 0 || bugActivityAssignments > 0)
        {
            TempData["Error"] =
                $"Cannot delete {user.FullName}. This user is referenced by existing records. " +
                $"Tasks created: {tasksCreated}, Projects created: {projectsCreated}, Bugs created: {bugsCreated}, " +
                $"Calendar events: {calendarEventsCreated}, Project PIC assignments: {projectPicAssignments}, " +
                $"Bug activity references: {bugActivityAssignments}.";
            return RedirectToAction(nameof(Employees));
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Employees));
        }

        TempData["Success"] = $"{user.FullName} deleted successfully.";
        return RedirectToAction(nameof(Employees));
    }

    private IActionResult? EnsureAdminAccess()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Challenge();

        return User.IsInRole("Admin") ? null : Forbid();
    }

    private async Task<AdminEmployeesViewModel> BuildEmployeesViewModel(RegisterViewModel? newEmployee = null)
    {
        var roles = new[] { "Employee", "Developer", "Tester", "Agent" };
        var items = new List<EmployeeListItem>();
        foreach (var role in roles)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);
            items.AddRange(users.Select(user => new EmployeeListItem { Employee = user, Role = role }));
        }

        items = items
            .OrderBy(e => e.Employee.FullName)
            .ToList();

        return new AdminEmployeesViewModel
        {
            Employees = items,
            NewEmployee = newEmployee ?? new RegisterViewModel()
        };
    }
}
