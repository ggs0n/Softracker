using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Developer")]
public class DeveloperController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDeveloperSummaryService _developerSummaryService;

    public DeveloperController(
        UserManager<ApplicationUser> userManager,
        IDeveloperSummaryService developerSummaryService)
    {
        _userManager = userManager;
        _developerSummaryService = developerSummaryService;
    }

    public async Task<IActionResult> Summary()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();
        var vm = await _developerSummaryService.BuildSummaryAsync(userId);
        return View(vm);
    }
}
