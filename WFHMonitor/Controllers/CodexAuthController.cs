using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin")]
public sealed class CodexAuthController(ICodexAuthService codexAuthService) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Connect(string? returnUrl, CancellationToken cancellationToken)
    {
        var result = await codexAuthService.StartLoginAsync(cancellationToken);
        TempData[result.Started ? "Info" : "Error"] = result.Message;
        return RedirectToLocal(returnUrl);
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
}
