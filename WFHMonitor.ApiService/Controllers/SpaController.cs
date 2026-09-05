using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[ApiController]
[Route("api/spa")]
public sealed class SpaController : ControllerBase
{
    private readonly IAntiforgery _antiforgery;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly IGitHubOAuthService _gitHubOAuthService;
    private readonly ILogger<SpaController> _logger;

    public SpaController(
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        ISystemSettingsService systemSettingsService,
        IGitHubOAuthService gitHubOAuthService,
        ILogger<SpaController> logger)
    {
        _antiforgery = antiforgery;
        _userManager = userManager;
        _notificationService = notificationService;
        _systemSettingsService = systemSettingsService;
        _gitHubOAuthService = gitHubOAuthService;
        _logger = logger;
    }

    [HttpGet("bootstrap")]
    [AllowAnonymous]
    public async Task<ActionResult<SpaBootstrapResponse>> Bootstrap()
    {
        try
        {
            var antiForgeryToken = _antiforgery.GetAndStoreTokens(HttpContext).RequestToken ?? string.Empty;
            if (User.Identity?.IsAuthenticated != true)
            {
                return Ok(SpaBootstrapResponse.Anonymous(antiForgeryToken));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = string.IsNullOrWhiteSpace(userId)
                ? null
                : await _userManager.FindByIdAsync(userId);

            if (user is null)
                return Unauthorized();

            var roles = await _userManager.GetRolesAsync(user);
            var access = await _systemSettingsService.BuildRuntimeAccessAsync(User);
            var notifications = await _notificationService.GetBellAsync(user.Id);
            var canManageGitHub = roles.Any(role =>
                role is "Admin" or "Developer" or "Employee");
            var gitHubConfigured = _gitHubOAuthService.IsOAuthConfigured();
            var gitHubConnected = canManageGitHub
                                  && gitHubConfigured
                                  && await _gitHubOAuthService.IsConnectedAsync(user.Id);

            var hasProAccess = user.SubscriptionPlan == SubscriptionPlan.Pro
                               && user.IsProSubscriptionActive
                               && (!user.ProSubscriptionEndsAt.HasValue
                                   || user.ProSubscriptionEndsAt.Value > DateTime.UtcNow);

            var profilePhotoFile = string.IsNullOrWhiteSpace(user.ProfilePhotoPath)
                ? null
                : Path.GetFileName(user.ProfilePhotoPath);

            return Ok(new SpaBootstrapResponse(
                true,
                antiForgeryToken,
                new SpaUserProfile(
                    user.Id,
                    string.IsNullOrWhiteSpace(user.FullName) ? user.Email ?? "User" : user.FullName,
                    user.Email ?? string.Empty,
                    profilePhotoFile is null ? null : $"/uploads/profiles/{profilePhotoFile}",
                    user.CompanyName,
                    user.SubscriptionPlan.ToString(),
                    hasProAccess),
                roles.ToArray(),
                access,
                notifications,
                gitHubConfigured,
                gitHubConnected));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not create the SPA bootstrap response.");
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "The application session could not be loaded.");
        }
    }
}

public sealed record SpaBootstrapResponse(
    bool IsAuthenticated,
    string AntiForgeryToken,
    SpaUserProfile? User,
    IReadOnlyList<string> Roles,
    RuntimeSystemAccessViewModel Access,
    NotificationBellViewModel Notifications,
    bool IsGitHubOAuthConfigured,
    bool IsGitHubConnected)
{
    public static SpaBootstrapResponse Anonymous(string antiForgeryToken)
    {
        return new SpaBootstrapResponse(
            false,
            antiForgeryToken,
            null,
            [],
            new RuntimeSystemAccessViewModel(),
            new NotificationBellViewModel(),
            false,
            false);
    }
}

public sealed record SpaUserProfile(
    string Id,
    string Name,
    string Email,
    string? PhotoUrl,
    string? CompanyName,
    string Plan,
    bool HasProAccess);
