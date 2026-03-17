using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin,Tester,Developer")]
public class MonitorController : Controller
{
    private readonly IProjectMonitoringService _projectMonitoringService;
    private readonly UserManager<ApplicationUser> _userManager;

    public MonitorController(
        IProjectMonitoringService projectMonitoringService,
        UserManager<ApplicationUser> userManager)
    {
        _projectMonitoringService = projectMonitoringService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var vm = await _projectMonitoringService.BuildDashboardAsync(
            User.IsInRole("Admin"),
            currentUser?.OrgTeamId,
            cancellationToken);
        ViewData["Title"] = "Monitor";
        return View(vm);
    }
}
