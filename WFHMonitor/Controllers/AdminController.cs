using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> AddEmployee(RegisterViewModel model)
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

    private async Task<AdminEmployeesViewModel> BuildEmployeesViewModel(RegisterViewModel? newEmployee = null)
    {
        var roles = new[] { "Employee", "Developer", "Tester" };
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
