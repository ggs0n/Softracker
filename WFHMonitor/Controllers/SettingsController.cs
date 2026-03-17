using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin")]
public class SettingsController : Controller
{
    private readonly ISystemSettingsService _settingsService;

    public SettingsController(ISystemSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? menu)
    {
        var vm = await _settingsService.BuildSettingsPageAsync(menu);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveModulePermissions(SettingsPageViewModel model)
    {
        await _settingsService.SaveModulePermissionsAsync(model.Modules ?? []);
        TempData["Success"] = "Module permissions updated.";
        return RedirectToAction(nameof(Index), new { menu = "ModulePermission" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBellNotification(SettingsPageViewModel model)
    {
        await _settingsService.SaveBellNotificationSettingsAsync(
            model.BellNotificationSoundEnabled,
            model.BellNotificationSoundOption);
        TempData["Success"] = "Bell notification settings updated.";
        return RedirectToAction(nameof(Index), new { menu = "BellNotification" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProVersion(SettingsPageViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide valid PRO Version limits.";
            return RedirectToAction(nameof(Index), new { menu = "ProVersion" });
        }

        await _settingsService.SaveProVersionSettingsAsync(model.ProVersion);
        TempData["Success"] = "PRO Version settings updated.";
        return RedirectToAction(nameof(Index), new { menu = "ProVersion" });
    }
}
