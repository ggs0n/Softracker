using System.Security.Claims;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface ISystemSettingsService
{
    Task<SettingsPageViewModel> BuildSettingsPageAsync(string? activeMenu = null);
    Task SaveModulePermissionsAsync(IReadOnlyCollection<ModulePermissionEditItemViewModel> modules);
    Task SaveBellNotificationSettingsAsync(bool enabled, string? soundOption);
    Task<bool> CanViewModuleAsync(ClaimsPrincipal user, string moduleKey);
    Task<bool> CanModifyModuleAsync(ClaimsPrincipal user, string moduleKey);
    Task<RuntimeSystemAccessViewModel> BuildRuntimeAccessAsync(ClaimsPrincipal user);
}
