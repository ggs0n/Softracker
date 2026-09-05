using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin,Developer,Employee")]
public class GitHubAuthController : Controller
{
    private readonly IGitHubOAuthService _gitHubOAuthService;

    public GitHubAuthController(IGitHubOAuthService gitHubOAuthService)
    {
        _gitHubOAuthService = gitHubOAuthService;
    }

    [HttpGet]
    public async Task<IActionResult> Connect(string? returnUrl = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToAction("Login", "Auth");

        if (!_gitHubOAuthService.IsOAuthConfigured())
        {
            TempData["Error"] = "GitHub OAuth is not configured. Please add ClientId, ClientSecret, and RedirectUri in appsettings.";
            return LocalRedirect(NormalizeReturnUrl(returnUrl));
        }

        var authorizeUrl = await _gitHubOAuthService.BuildAuthorizeUrlAsync(userId, returnUrl);
        return Redirect(authorizeUrl);
    }

    [HttpGet]
    public async Task<IActionResult> Callback(string? code = null, string? state = null, string? error = null, string? error_description = null)
    {
        var fallbackReturnUrl = "/ChangeRequest/Create";

        if (!string.IsNullOrWhiteSpace(error))
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(error_description)
                ? $"GitHub authorization failed: {error}"
                : $"GitHub authorization failed: {error_description}";
            return LocalRedirect(fallbackReturnUrl);
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            TempData["Error"] = "GitHub authorization failed: missing callback parameters.";
            return LocalRedirect(fallbackReturnUrl);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return RedirectToAction("Login", "Auth");

        var result = await _gitHubOAuthService.CompleteAuthorizationAsync(code, state, userId);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.ErrorMessage ?? "GitHub authorization failed.";
            return LocalRedirect(NormalizeReturnUrl(result.ReturnUrl));
        }

        TempData["Success"] = "GitHub connected successfully.";
        return LocalRedirect(NormalizeReturnUrl(result.ReturnUrl));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disconnect(string? returnUrl = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
            await _gitHubOAuthService.DisconnectAsync(userId);

        TempData["Success"] = "GitHub connection removed.";
        return LocalRedirect(NormalizeReturnUrl(returnUrl));
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/ChangeRequest";

        var normalized = returnUrl.Trim();
        if (normalized.StartsWith("/", StringComparison.Ordinal)
            && !normalized.StartsWith("//", StringComparison.Ordinal)
            && !normalized.StartsWith("/\\", StringComparison.Ordinal))
        {
            return normalized;
        }

        return "/ChangeRequest";
    }
}
