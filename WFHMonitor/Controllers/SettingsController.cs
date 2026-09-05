using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin")]
public class SettingsController : Controller
{
    private readonly ISystemSettingsService _settingsService;
    private readonly ICodexModelCatalogService _codexModelCatalog;

    public SettingsController(
        ISystemSettingsService settingsService,
        ICodexModelCatalogService codexModelCatalog)
    {
        _settingsService = settingsService;
        _codexModelCatalog = codexModelCatalog;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCodexAi(SettingsPageViewModel model)
    {
        if (!ModelState.IsValid ||
            !WFHMonitor.Models.CodexAiDefaults.ReasoningEfforts.Contains(
                model.CodexAi.ReasoningEffort,
                StringComparer.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Please choose a valid Codex model and reasoning level.";
            return RedirectToAction(nameof(Index), new { menu = "CodexAi" });
        }

        await _settingsService.SaveCodexAiSettingsAsync(model.CodexAi);
        TempData["Success"] = "Softracker Codex settings updated. Your VS Code Codex settings were not changed.";
        return RedirectToAction(nameof(Index), new { menu = "CodexAi" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshCodexModels(CancellationToken cancellationToken)
    {
        var result = await _codexModelCatalog.GetModelsAsync(forceRefresh: true, cancellationToken);
        TempData[result.IsLive ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index), new { menu = "CodexAi" });
    }
}
