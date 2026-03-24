using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public class OnboardingService : IOnboardingService
{
    private readonly ApplicationDbContext _db;
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly IGitHubOAuthService _gitHubOAuthService;

    public OnboardingService(
        ApplicationDbContext db,
        ISystemSettingsService systemSettingsService,
        IGitHubOAuthService gitHubOAuthService)
    {
        _db = db;
        _systemSettingsService = systemSettingsService;
        _gitHubOAuthService = gitHubOAuthService;
    }

    public async Task<OnboardingStateViewModel> GetStateAsync(ClaimsPrincipal user)
    {
        if (!TryGetEligibleUserId(user, out var userId))
            return CreateIneligibleState();

        var runtimeAccess = await _systemSettingsService.BuildRuntimeAccessAsync(user);
        var canViewProjects = runtimeAccess.CanViewAllProjects;
        var canUseGitHubOAuth = _gitHubOAuthService.IsOAuthConfigured();

        var projectQuery = _db.ChangeRequests
            .AsNoTracking()
            .Where(c => c.CreatedById == userId);

        var hasProject = await projectQuery.AnyAsync();
        var hasGitHubAuthorization = canUseGitHubOAuth
            ? await _gitHubOAuthService.IsConnectedAsync(userId)
            : false;

        var step1Complete = hasProject;
        var step2Complete = hasGitHubAuthorization;
        var step3Complete = step1Complete && step2Complete;

        var entity = await GetOrCreateStateAsync(userId);
        var changed = false;

        if (step3Complete && !entity.IsCompleted)
        {
            entity.IsCompleted = true;
            entity.CompletedAt = DateTime.UtcNow;
            entity.IsDismissed = false;
            entity.DismissedAt = null;
            changed = true;
        }
        else if (!step3Complete && entity.IsCompleted)
        {
            entity.IsCompleted = false;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(entity.LastSeenStepKey))
        {
            entity.LastSeenStepKey = OnboardingStepKeys.AddProject;
            changed = true;
        }

        if (changed)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        var step1 = new OnboardingStepViewModel
        {
            Key = OnboardingStepKeys.AddProject,
            Title = "Add New Project",
            Description = "Create your first project so your team can start tracking work.",
            IsComplete = step1Complete,
            HasAccess = canViewProjects,
            TargetSelector = "[data-onboarding-id='project-create-entry']",
            TargetUrl = "/ChangeRequest",
            TargetLabel = "Go to Projects"
        };

        var step2 = new OnboardingStepViewModel
        {
            Key = OnboardingStepKeys.AddGitHubIntegration,
            Title = "Add GitHub Integration",
            Description = canUseGitHubOAuth
                ? "Connect your GitHub account so adib can access repositories securely."
                : "GitHub OAuth is not configured yet. Ask admin to add GitHubOAuth settings.",
            IsComplete = step2Complete,
            HasAccess = canUseGitHubOAuth,
            NoAccessMessage = canUseGitHubOAuth ? null : "GitHub OAuth is disabled in app settings.",
            TargetSelector = "[data-onboarding-id='github-connect-button']",
            TargetUrl = canUseGitHubOAuth ? "/GitHubAuth/Connect?returnUrl=%2FChangeRequest%2FCreate" : "/Settings",
            TargetLabel = canUseGitHubOAuth ? "Connect GitHub" : "Open Settings"
        };

        var step3 = new OnboardingStepViewModel
        {
            Key = OnboardingStepKeys.Done,
            Title = "Done",
            Description = "Your onboarding is complete. You can replay this tour anytime from the topbar Help button.",
            IsComplete = step3Complete,
            HasAccess = true,
            TargetSelector = "[data-onboarding-id='start-tour-button']",
            TargetUrl = "/",
            TargetLabel = "Go to Dashboard"
        };

        var steps = new List<OnboardingStepViewModel> { step1, step2, step3 };
        var currentStepKey = ResolveCurrentStepKey(steps, entity.LastSeenStepKey);
        var currentStepIndex = Math.Max(0, steps.FindIndex(s => s.Key == currentStepKey));

        return new OnboardingStateViewModel
        {
            IsEligible = true,
            IsDismissed = entity.IsDismissed,
            IsCompleted = step3Complete,
            IsVisible = !entity.IsDismissed && !step3Complete,
            LastSeenStepKey = entity.LastSeenStepKey,
            CurrentStepKey = currentStepKey,
            CurrentStepIndex = currentStepIndex,
            Steps = steps
        };
    }

    public async Task<OnboardingStateViewModel> DismissAsync(ClaimsPrincipal user, string? lastSeenStepKey)
    {
        if (!TryGetEligibleUserId(user, out var userId))
            return CreateIneligibleState();

        var entity = await GetOrCreateStateAsync(userId);
        entity.IsDismissed = true;
        entity.DismissedAt = DateTime.UtcNow;
        entity.LastSeenStepKey = NormalizeStepKey(lastSeenStepKey);
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await GetStateAsync(user);
    }

    public async Task<OnboardingStateViewModel> RestartAsync(ClaimsPrincipal user)
    {
        if (!TryGetEligibleUserId(user, out var userId))
            return CreateIneligibleState();

        var entity = await GetOrCreateStateAsync(userId);
        entity.IsDismissed = false;
        entity.DismissedAt = null;
        entity.LastSeenStepKey = OnboardingStepKeys.AddProject;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var state = await GetStateAsync(user);
        state.CurrentStepKey = OnboardingStepKeys.AddProject;
        state.CurrentStepIndex = 0;
        return state;
    }

    private async Task<UserOnboardingState> GetOrCreateStateAsync(string userId)
    {
        var state = await _db.UserOnboardingStates
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (state != null)
            return state;

        state = new UserOnboardingState
        {
            UserId = userId,
            LastSeenStepKey = OnboardingStepKeys.AddProject
        };

        _db.UserOnboardingStates.Add(state);
        await _db.SaveChangesAsync();
        return state;
    }

    private static string ResolveCurrentStepKey(IReadOnlyList<OnboardingStepViewModel> steps, string? lastSeenStepKey)
    {
        var firstIncompleteAccessible = steps.FirstOrDefault(s => !s.IsComplete && s.HasAccess);
        if (firstIncompleteAccessible is not null)
            return firstIncompleteAccessible.Key;

        var firstIncomplete = steps.FirstOrDefault(s => !s.IsComplete);
        if (firstIncomplete is not null)
            return firstIncomplete.Key;

        var normalizedLastSeen = NormalizeStepKey(lastSeenStepKey);
        if (steps.Any(s => s.Key == normalizedLastSeen))
            return normalizedLastSeen;

        return OnboardingStepKeys.Done;
    }

    private static string NormalizeStepKey(string? stepKey)
    {
        if (string.IsNullOrWhiteSpace(stepKey))
            return OnboardingStepKeys.AddProject;

        var normalized = stepKey.Trim().ToLowerInvariant();
        return OnboardingStepKeys.Ordered.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? normalized
            : OnboardingStepKeys.AddProject;
    }

    private static bool TryGetEligibleUserId(ClaimsPrincipal user, out string userId)
    {
        if (user is null)
        {
            userId = string.Empty;
            return false;
        }

        userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return user.IsInRole("Admin") || user.IsInRole("Developer") || user.IsInRole("Employee");
    }

    private static OnboardingStateViewModel CreateIneligibleState()
    {
        return new OnboardingStateViewModel
        {
            IsEligible = false,
            IsVisible = false,
            IsDismissed = true,
            IsCompleted = false,
            CurrentStepKey = OnboardingStepKeys.AddProject,
            CurrentStepIndex = 0,
            LastSeenStepKey = OnboardingStepKeys.AddProject
        };
    }
}
