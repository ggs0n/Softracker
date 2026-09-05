using System.Security.Claims;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface IOnboardingService
{
    Task<OnboardingStateViewModel> GetStateAsync(ClaimsPrincipal user);
    Task<OnboardingStateViewModel> DismissAsync(ClaimsPrincipal user, string? lastSeenStepKey);
    Task<OnboardingStateViewModel> RestartAsync(ClaimsPrincipal user);
}
