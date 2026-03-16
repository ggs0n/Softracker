using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin,Tester,Developer")]
public class MonitorController : Controller
{
    private readonly IProjectMonitoringService _projectMonitoringService;

    public MonitorController(IProjectMonitoringService projectMonitoringService)
    {
        _projectMonitoringService = projectMonitoringService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var vm = await _projectMonitoringService.BuildDashboardAsync(cancellationToken);
        ViewData["Title"] = "Monitor";
        return View(vm);
    }
}
