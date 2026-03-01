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
        var today = DateTime.UtcNow.Date;

        var employees = await _userManager.GetUsersInRoleAsync("Employee");

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

        var vm = new AdminDashboardViewModel
        {
            Today = today,
            TasksDoneToday = tasksDoneToday,
            BlockedTasks = blockedTasks,
            TotalEmployees = employees.Count
        };

        return View(vm);
    }

    public async Task<IActionResult> Employees()
    {
        var vm = await BuildEmployeesViewModel();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEmployee([Bind(Prefix = "NewEmployee")] RegisterViewModel model)
    {
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
    public async Task<IActionResult> DeleteEmployee(string userId)
    {
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
