using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize]
public class OnboardingController : Controller
{
    private readonly IOnboardingService _onboardingService;
    private readonly IUserRegistrationService _userRegistrationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public OnboardingController(
        IOnboardingService onboardingService,
        IUserRegistrationService userRegistrationService,
        UserManager<ApplicationUser> userManager)
    {
        _onboardingService = onboardingService;
        _userRegistrationService = userRegistrationService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> State()
    {
        var state = await _onboardingService.GetStateAsync(User);
        return Json(state);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(string? lastSeenStepKey)
    {
        var state = await _onboardingService.DismissAsync(User, lastSeenStepKey);
        return Json(state);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restart()
    {
        var state = await _onboardingService.RestartAsync(User);
        return Json(state);
    }

    [HttpGet]
    public async Task<IActionResult> Welcome(string? returnUrl = null)
    {
        var state = await _onboardingService.GetStateAsync(User);
        if (!state.IsEligible)
            return RedirectToAction("Index", "Home");

        var fallbackReturnUrl = User.IsInRole("Admin") ? "/Admin" : "/ChangeRequest";
        var normalizedReturnUrl = NormalizeReturnUrl(returnUrl, fallbackReturnUrl);
        var gitHubStep = state.Steps.FirstOrDefault(s => s.Key == OnboardingStepKeys.AddGitHubIntegration);

        if (state.IsCompleted || gitHubStep?.IsComplete == true)
            return LocalRedirect(normalizedReturnUrl);

        var setupTeamUrl = $"/Onboarding/SetupTeam?returnUrl={Uri.EscapeDataString(normalizedReturnUrl)}";
        var connectUrl = gitHubStep?.HasAccess == true
            ? $"/GitHubAuth/Connect?returnUrl={Uri.EscapeDataString(setupTeamUrl)}"
            : "/Settings";

        var model = new OnboardingWelcomeViewModel
        {
            CanUseGitHubOAuth = gitHubStep?.HasAccess == true,
            IsGitHubConnected = gitHubStep?.IsComplete == true,
            ConnectUrl = connectUrl,
            SkipUrl = "/Onboarding/SkipWelcome",
            ReturnUrl = normalizedReturnUrl,
            NoAccessMessage = gitHubStep?.NoAccessMessage
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SkipWelcome(string? returnUrl = null)
    {
        await _onboardingService.DismissAsync(User, OnboardingStepKeys.AddGitHubIntegration);

        var fallbackReturnUrl = User.IsInRole("Admin") ? "/Admin" : "/ChangeRequest";
        var normalizedReturnUrl = NormalizeReturnUrl(returnUrl, fallbackReturnUrl);
        return RedirectToAction("SetupTeam", new { returnUrl = normalizedReturnUrl });
    }

    [HttpGet]
    public IActionResult SetupTeam(string? returnUrl = null)
    {
        var fallbackReturnUrl = User.IsInRole("Admin") ? "/Admin" : "/ChangeRequest";
        var model = new OnboardingSetupTeamViewModel
        {
            ReturnUrl = NormalizeReturnUrl(returnUrl, fallbackReturnUrl)
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetupTeam(OnboardingSetupTeamViewModel model)
    {
        var fallbackReturnUrl = User.IsInRole("Admin") ? "/Admin" : "/ChangeRequest";
        var normalizedReturnUrl = NormalizeReturnUrl(model.ReturnUrl, fallbackReturnUrl);

        if (model.Members == null || model.Members.Count == 0)
            return LocalRedirect(normalizedReturnUrl);

        var currentUser = await _userManager.GetUserAsync(User);
        var companyName = currentUser?.CompanyName;

        var created = 0;
        var errors = new List<string>();
        foreach (var member in model.Members)
        {
            if (string.IsNullOrWhiteSpace(member.Email))
                continue;

            var registerModel = new RegisterViewModel
            {
                Email = member.Email.Trim(),
                Password = "abc123",
                ConfirmPassword = "abc123",
                FullName = member.Email.Split('@')[0].Replace(".", " ").Replace("_", " "),
                CompanyName = companyName,
                Role = string.IsNullOrWhiteSpace(member.Role) ? "Employee" : member.Role
            };

            var result = await _userRegistrationService.RegisterAsync(registerModel);
            if (result.Succeeded)
                created++;
            else
                errors.AddRange(result.Errors);
        }

        if (created > 0)
            TempData["Success"] = $"{created} team member(s) added successfully. Default password: abc123";
        if (errors.Count > 0)
            TempData["Error"] = string.Join("; ", errors.Take(3));

        await _onboardingService.DismissAsync(User, "done");
        return LocalRedirect(normalizedReturnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SkipSetupTeam(string? returnUrl = null)
    {
        await _onboardingService.DismissAsync(User, "done");
        var fallbackReturnUrl = User.IsInRole("Admin") ? "/Admin" : "/ChangeRequest";
        return LocalRedirect(NormalizeReturnUrl(returnUrl, fallbackReturnUrl));
    }

    private static string NormalizeReturnUrl(string? returnUrl, string fallbackReturnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return fallbackReturnUrl;

        var normalized = returnUrl.Trim();
        if (normalized.StartsWith("/", StringComparison.Ordinal)
            && !normalized.StartsWith("//", StringComparison.Ordinal)
            && !normalized.StartsWith("/\\", StringComparison.Ordinal))
        {
            return normalized;
        }

        return fallbackReturnUrl;
    }
}
