using System.Security.Claims;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface ISystemSettingsService
{
    Task<SettingsPageViewModel> BuildSettingsPageAsync(string? activeMenu = null);
    Task<ProVersionSettingsViewModel> GetProVersionSettingsAsync();
    Task<CodexAiSettingsViewModel> GetCodexAiSettingsAsync();
    Task SaveModulePermissionsAsync(IReadOnlyCollection<ModulePermissionEditItemViewModel> modules);
    Task SaveBellNotificationSettingsAsync(bool enabled, string? soundOption);
    Task SaveProVersionSettingsAsync(ProVersionSettingsViewModel model);
    Task SaveCodexAiSettingsAsync(CodexAiSettingsViewModel model);
    Task<bool> CanViewModuleAsync(ClaimsPrincipal user, string moduleKey);
    Task<bool> CanModifyModuleAsync(ClaimsPrincipal user, string moduleKey);
    Task<RuntimeSystemAccessViewModel> BuildRuntimeAccessAsync(ClaimsPrincipal user);
}
