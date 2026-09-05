using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin")]
public sealed class CodexAuthController(ICodexAuthService codexAuthService) : Controller
{
    [HttpGet]
    public IActionResult Connect(string? returnUrl)
    {
        SetConnectViewData(returnUrl, loginAttempted: false, loginStarted: false,
            "Preparing the Codex sign-in flow...");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Connect(string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await codexAuthService.StartLoginAsync(cancellationToken);
        SetConnectViewData(returnUrl, loginAttempted: true, result.Started, result.Message);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var status = await codexAuthService.GetStatusAsync(cancellationToken);
        return Json(new
        {
            available = status.IsAvailable,
            authenticated = status.IsAuthenticated,
            message = status.Message
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect(string? returnUrl, CancellationToken cancellationToken)
    {
        var succeeded = await codexAuthService.LogoutAsync(cancellationToken);
        TempData[succeeded ? "Success" : "Error"] = succeeded
            ? "Codex disconnected."
            : "Unable to disconnect Codex.";
        return RedirectToLocal(returnUrl);
    }

    private IActionResult RedirectToLocal(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Home");

    private void SetConnectViewData(
        string? returnUrl,
        bool loginAttempted,
        bool loginStarted,
        string message)
    {
        ViewBag.ReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Action("Index", "Home") ?? "/";
        ViewBag.LoginAttempted = loginAttempted;
        ViewBag.LoginStarted = loginStarted;
        ViewBag.Message = message;
    }
}
