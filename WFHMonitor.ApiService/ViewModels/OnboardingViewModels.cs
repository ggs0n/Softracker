namespace WFHMonitor.ViewModels;

public class OnboardingStateViewModel
{
    public bool IsEligible { get; set; }
    public bool IsVisible { get; set; }
    public bool IsDismissed { get; set; }
    public bool IsCompleted { get; set; }
    public string CurrentStepKey { get; set; } = string.Empty;
    public int CurrentStepIndex { get; set; }
    public string LastSeenStepKey { get; set; } = string.Empty;
    public List<OnboardingStepViewModel> Steps { get; set; } = new();
}

public class OnboardingStepViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public bool HasAccess { get; set; } = true;
    public string? NoAccessMessage { get; set; }
    public string? TargetSelector { get; set; }
    public string? TargetUrl { get; set; }
    public string? TargetLabel { get; set; }
}

public class OnboardingWelcomeViewModel
{
    public bool CanUseGitHubOAuth { get; set; }
    public bool IsGitHubConnected { get; set; }
    public string ConnectUrl { get; set; } = string.Empty;
    public string SkipUrl { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string? NoAccessMessage { get; set; }
}

public class OnboardingSetupTeamViewModel
{
    public string ReturnUrl { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public List<OnboardingMemberRow> Members { get; set; } = new();
}

public class OnboardingMemberRow
{
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Employee";
}
