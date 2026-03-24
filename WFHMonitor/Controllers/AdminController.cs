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

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Index()
    {
        var accessDenied = EnsureAdminAccess();
        if (accessDenied != null) return accessDenied;

        var currentAdmin = await _userManager.GetUserAsync(User);
        var currentAdminId = currentAdmin?.Id ?? string.Empty;
        var currentCompanyName = NormalizeCompanyName(currentAdmin?.CompanyName);

        var today = DateTime.UtcNow.Date;

        var teamRoles = new[] { "Employee", "Developer", "Tester", "Agent" };
        var teamMemberIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var agentIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var developerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in teamRoles)
        {
            var users = (await _userManager.GetUsersInRoleAsync(role))
                .Where(u => u.IsActive)
                .Where(u => IsCompanyVisibleToAdmin(u.CompanyName, currentCompanyName, currentAdminId, u.Id))
                .ToList();
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

        var companyProjectsQuery = _db.ChangeRequests
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(currentCompanyName))
            companyProjectsQuery = companyProjectsQuery.Where(c => c.CreatedBy != null && c.CreatedBy.CompanyName == currentCompanyName);
        else if (!string.IsNullOrWhiteSpace(currentAdminId))
            companyProjectsQuery = companyProjectsQuery.Where(c => c.CreatedById == currentAdminId);

        var projects = await companyProjectsQuery
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync();

        var companyProjectIds = projects
            .Select(p => p.Id)
            .ToList();

        var tasksDoneToday = await _db.WorkTasks
            .Include(t => t.Assignee)
            .Where(t =>
                t.Status == WorkTaskStatus.Done &&
                t.UpdatedAt.Date == today &&
                t.ChangeRequestId.HasValue &&
                companyProjectIds.Contains(t.ChangeRequestId.Value))
            .AsNoTracking()
            .ToListAsync();

        var blockedTasks = await _db.WorkTasks
            .Include(t => t.Assignee)
            .Where(t =>
                t.Status == WorkTaskStatus.Blocked &&
                t.ChangeRequestId.HasValue &&
                companyProjectIds.Contains(t.ChangeRequestId.Value))
            .AsNoTracking()
            .ToListAsync();

        var allTasks = await _db.WorkTasks
            .Where(t => t.ChangeRequestId.HasValue && companyProjectIds.Contains(t.ChangeRequestId.Value))
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
            .Where(b => b.ChangeRequestId.HasValue && companyProjectIds.Contains(b.ChangeRequestId.Value))
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
            .Where(f => companyProjectIds.Contains(f.ChangeRequestId))
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
        var totalBugs = await _db.BugReports.CountAsync(b => b.ChangeRequestId.HasValue && companyProjectIds.Contains(b.ChangeRequestId.Value));
        var completeBugs = await _db.BugReports.CountAsync(b =>
            b.ChangeRequestId.HasValue &&
            companyProjectIds.Contains(b.ChangeRequestId.Value) &&
            b.Status == BugStatus.Complete);

        var agentFeaturesShipped = agentIdList.Count == 0
            ? 0
            : await _db.ProjectFeatures.CountAsync(f =>
                companyProjectIds.Contains(f.ChangeRequestId) &&
                f.AssignedDeveloperId != null &&
                agentIdList.Contains(f.AssignedDeveloperId) &&
                (f.AgentStatus == FeatureAgentStatus.PrRaised || f.Status == CrStatus.Done || f.IsCompleted));

        var agentBugsFound = await _db.BugReports.CountAsync(b =>
            b.ChangeRequestId.HasValue &&
            companyProjectIds.Contains(b.ChangeRequestId.Value) &&
            b.AssigneeType == BugAssigneeType.Agent);

        var agentBugsFixed = await _db.BugReports.CountAsync(b =>
            b.ChangeRequestId.HasValue &&
            companyProjectIds.Contains(b.ChangeRequestId.Value) &&
            b.AssigneeType == BugAssigneeType.Agent &&
            (b.AgentStatus == BugAgentStatus.PrRaised || b.Status == BugStatus.Complete));

        var developerBugsFixed = developerIdList.Count == 0
            ? 0
            : await _db.BugReports.CountAsync(b =>
                b.ChangeRequestId.HasValue &&
                companyProjectIds.Contains(b.ChangeRequestId.Value) &&
                b.AssigneeType == BugAssigneeType.Developer &&
                b.AssignedDeveloperId != null &&
                developerIdList.Contains(b.AssignedDeveloperId) &&
                b.Status == BugStatus.Complete);

        var developerFeaturesDelivered = developerIdList.Count == 0
            ? 0
            : await _db.ProjectFeatures.CountAsync(f =>
                companyProjectIds.Contains(f.ChangeRequestId) &&
                f.AssignedDeveloperId != null &&
                developerIdList.Contains(f.AssignedDeveloperId) &&
                (f.Status == CrStatus.Done || f.IsCompleted));

        var tasksDoneTodayComposite = agentBugsFixed + agentBugsFound + agentFeaturesShipped + (developerBugsFixed + developerFeaturesDelivered);

        // Day-by-day report (last 7 days) — tasks, bugs, and features
        var reportStartDate = today.AddDays(-6);

        var tasksDoneByDay = await _db.WorkTasks
            .Include(t => t.Assignee).Include(t => t.ChangeRequest)
            .Where(t => t.Status == WorkTaskStatus.Done
                && t.UpdatedAt.Date >= reportStartDate && t.UpdatedAt.Date <= today
                && t.ChangeRequestId.HasValue && companyProjectIds.Contains(t.ChangeRequestId.Value))
            .AsNoTracking().OrderByDescending(t => t.UpdatedAt).ToListAsync();

        var bugsDoneByDay = await _db.BugReports
            .Include(b => b.AssignedDeveloper).Include(b => b.ChangeRequest)
            .Where(b => b.Status == BugStatus.Complete
                && b.UpdatedAt.Date >= reportStartDate && b.UpdatedAt.Date <= today
                && b.ChangeRequestId.HasValue && companyProjectIds.Contains(b.ChangeRequestId.Value))
            .AsNoTracking().OrderByDescending(b => b.UpdatedAt).ToListAsync();

        var featuresDoneByDay = await _db.ProjectFeatures
            .Include(f => f.AssignedDeveloper).Include(f => f.ChangeRequest)
            .Where(f => f.IsCompleted
                && f.CreatedAt.Date >= reportStartDate && f.CreatedAt.Date <= today
                && companyProjectIds.Contains(f.ChangeRequestId))
            .AsNoTracking().OrderByDescending(f => f.CreatedAt).ToListAsync();

        var dailyReports = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(-i))
            .Select(date =>
            {
                var items = new List<DailyTaskItem>();

                items.AddRange(tasksDoneByDay
                    .Where(t => t.UpdatedAt.Date == date)
                    .Select(t => new DailyTaskItem
                    {
                        Id = t.Id, Title = t.Title, ItemType = "Task",
                        AssigneeName = t.Assignee?.FullName,
                        ProjectTitle = t.ChangeRequest?.Title,
                        ProjectCrNumber = t.ChangeRequest?.CrNumber,
                        ChangeRequestId = t.ChangeRequestId
                    }));

                items.AddRange(bugsDoneByDay
                    .Where(b => b.UpdatedAt.Date == date)
                    .Select(b => new DailyTaskItem
                    {
                        Id = b.Id, Title = b.Title, ItemType = "Bug",
                        ItemNumber = b.BugNumber,
                        AssigneeName = b.AssignedDeveloper?.FullName,
                        ProjectTitle = b.ChangeRequest?.Title,
                        ProjectCrNumber = b.ChangeRequest?.CrNumber,
                        ChangeRequestId = b.ChangeRequestId
                    }));

                items.AddRange(featuresDoneByDay
                    .Where(f => f.CreatedAt.Date == date)
                    .Select(f => new DailyTaskItem
                    {
                        Id = f.Id, Title = f.Name, ItemType = "Feature",
                        ItemNumber = f.FeatureNumber,
                        AssigneeName = f.AssignedDeveloper?.FullName,
                        ProjectTitle = f.ChangeRequest?.Title,
                        ProjectCrNumber = f.ChangeRequest?.CrNumber,
                        ChangeRequestId = f.ChangeRequestId
                    }));

                return new DailyTaskReport { Date = date, Tasks = items };
            })
            .ToList();

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
            DeveloperDeliveredItems = developerBugsFixed + developerFeaturesDelivered,
            DailyTaskReports = dailyReports
        };

        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportTasksPdf(int? days)
    {
        var accessDenied = EnsureAdminAccess();
        if (accessDenied != null) return accessDenied;

        var currentAdmin = await _userManager.GetUserAsync(User);
        var currentAdminId = currentAdmin?.Id ?? string.Empty;
        var currentCompanyName = NormalizeCompanyName(currentAdmin?.CompanyName);

        var today = DateTime.UtcNow.Date;
        var reportDays = days is > 0 and <= 30 ? days.Value : 7;
        var reportStartDate = today.AddDays(-(reportDays - 1));

        var companyProjectsQuery = _db.ChangeRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(currentCompanyName))
            companyProjectsQuery = companyProjectsQuery.Where(c => c.CreatedBy != null && c.CreatedBy.CompanyName == currentCompanyName);
        else if (!string.IsNullOrWhiteSpace(currentAdminId))
            companyProjectsQuery = companyProjectsQuery.Where(c => c.CreatedById == currentAdminId);

        var companyProjectIds = await companyProjectsQuery.Select(p => p.Id).ToListAsync();

        var tasksDone = await _db.WorkTasks
            .Include(t => t.Assignee).Include(t => t.ChangeRequest)
            .Where(t => t.Status == WorkTaskStatus.Done
                && t.UpdatedAt.Date >= reportStartDate && t.UpdatedAt.Date <= today
                && t.ChangeRequestId.HasValue && companyProjectIds.Contains(t.ChangeRequestId.Value))
            .AsNoTracking().OrderByDescending(t => t.UpdatedAt).ToListAsync();

        var bugsDone = await _db.BugReports
            .Include(b => b.AssignedDeveloper).Include(b => b.ChangeRequest)
            .Where(b => b.Status == BugStatus.Complete
                && b.UpdatedAt.Date >= reportStartDate && b.UpdatedAt.Date <= today
                && b.ChangeRequestId.HasValue && companyProjectIds.Contains(b.ChangeRequestId.Value))
            .AsNoTracking().OrderByDescending(b => b.UpdatedAt).ToListAsync();

        var featuresDone = await _db.ProjectFeatures
            .Include(f => f.AssignedDeveloper).Include(f => f.ChangeRequest)
            .Where(f => f.IsCompleted
                && f.CreatedAt.Date >= reportStartDate && f.CreatedAt.Date <= today
                && companyProjectIds.Contains(f.ChangeRequestId))
            .AsNoTracking().OrderByDescending(f => f.CreatedAt).ToListAsync();

        var dailyReports = Enumerable.Range(0, reportDays)
            .Select(i => today.AddDays(-i))
            .Select(date =>
            {
                var items = new List<DailyTaskItem>();
                items.AddRange(tasksDone.Where(t => t.UpdatedAt.Date == date).Select(t => new DailyTaskItem
                {
                    Id = t.Id, Title = t.Title, ItemType = "Task",
                    AssigneeName = t.Assignee?.FullName, ProjectTitle = t.ChangeRequest?.Title,
                    ProjectCrNumber = t.ChangeRequest?.CrNumber, ChangeRequestId = t.ChangeRequestId
                }));
                items.AddRange(bugsDone.Where(b => b.UpdatedAt.Date == date).Select(b => new DailyTaskItem
                {
                    Id = b.Id, Title = b.Title, ItemType = "Bug", ItemNumber = b.BugNumber,
                    AssigneeName = b.AssignedDeveloper?.FullName, ProjectTitle = b.ChangeRequest?.Title,
                    ProjectCrNumber = b.ChangeRequest?.CrNumber, ChangeRequestId = b.ChangeRequestId
                }));
                items.AddRange(featuresDone.Where(f => f.CreatedAt.Date == date).Select(f => new DailyTaskItem
                {
                    Id = f.Id, Title = f.Name, ItemType = "Feature", ItemNumber = f.FeatureNumber,
                    AssigneeName = f.AssignedDeveloper?.FullName, ProjectTitle = f.ChangeRequest?.Title,
                    ProjectCrNumber = f.ChangeRequest?.CrNumber, ChangeRequestId = f.ChangeRequestId
                }));
                return new DailyTaskReport { Date = date, Tasks = items };
            })
            .ToList();

        var totalItems = tasksDone.Count + bugsDone.Count + featuresDone.Count;
        ViewBag.CompanyName = currentCompanyName ?? "Softracker";
        ViewBag.ReportDays = reportDays;
        ViewBag.GeneratedAt = DateTime.UtcNow;
        ViewBag.TotalTasksDone = totalItems;

        return View(dailyReports);
    }

    [Authorize(Roles = "Admin")]
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

        var currentAdmin = await _userManager.GetUserAsync(User);
        model.CompanyName = NormalizeCompanyName(currentAdmin?.CompanyName);

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

        var currentAdmin = await _userManager.GetUserAsync(User);
        if (!IsCompanyVisibleToAdmin(
                user.CompanyName,
                NormalizeCompanyName(currentAdmin?.CompanyName),
                currentAdmin?.Id,
                user.Id))
        {
            TempData["Error"] = "You cannot manage users from another company.";
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
        var currentAdmin = await _userManager.GetUserAsync(User);
        var currentAdminId = currentAdmin?.Id ?? string.Empty;
        var currentCompanyName = NormalizeCompanyName(currentAdmin?.CompanyName);
        var roles = new[] { "Employee", "Developer", "Tester", "Agent" };
        var items = new List<EmployeeListItem>();
        foreach (var role in roles)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);
            items.AddRange(users
                .Where(user => user.IsActive)
                .Where(user => IsCompanyVisibleToAdmin(user.CompanyName, currentCompanyName, currentAdminId, user.Id))
                .Select(user => new EmployeeListItem { Employee = user, Role = role }));
        }

        items = items
            .OrderBy(e => e.Employee.FullName)
            .ToList();

        return new AdminEmployeesViewModel
        {
            Employees = items,
            NewEmployee = newEmployee ?? new RegisterViewModel { CompanyName = currentCompanyName }
        };
    }

    private static string? NormalizeCompanyName(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static bool IsCompanyVisibleToAdmin(string? targetCompanyName, string? adminCompanyName, string? adminUserId, string? targetUserId)
    {
        if (!string.IsNullOrWhiteSpace(adminCompanyName))
            return string.Equals(NormalizeCompanyName(targetCompanyName), adminCompanyName, StringComparison.OrdinalIgnoreCase);

        return !string.IsNullOrWhiteSpace(adminUserId) &&
               !string.IsNullOrWhiteSpace(targetUserId) &&
               string.Equals(adminUserId, targetUserId, StringComparison.Ordinal);
    }
}
