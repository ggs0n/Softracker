using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private static readonly string[] RoleOrder = ["Admin", "Tester", "Developer", "Agent", "Employee"];
    private const string PermissionsCacheKey = "sys:permissions";
    private const string PreferenceCacheKey = "sys:preference";

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public SystemSettingsService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<SettingsPageViewModel> BuildSettingsPageAsync(string? activeMenu = null)
    {
        var byKey = await GetCachedPermissionsAsync();

        var modules = AppModuleKeys.All
            .Select(moduleKey => BuildPermissionEditor(moduleKey, byKey.GetValueOrDefault(moduleKey)))
            .ToList();

        var pref = await GetOrCreatePreferenceAsync(trackChanges: false);
        return new SettingsPageViewModel
        {
            ActiveMenu = NormalizeActiveMenu(activeMenu),
            Modules = modules,
            BellNotificationSoundEnabled = pref.BellNotificationSoundEnabled,
            BellNotificationSoundOption = NormalizeSoundOption(pref.BellNotificationSoundOption),
            ProVersion = BuildProVersionSettings(pref)
        };
    }

    public async Task<ProVersionSettingsViewModel> GetProVersionSettingsAsync()
    {
        var pref = await GetOrCreatePreferenceAsync(trackChanges: false);
        return BuildProVersionSettings(pref);
    }

    public async Task SaveModulePermissionsAsync(IReadOnlyCollection<ModulePermissionEditItemViewModel> modules)
    {
        var existing = await _db.ModulePermissionSettings.ToListAsync();
        var now = DateTime.UtcNow;

        foreach (var item in modules)
        {
            if (!AppModuleKeys.All.Contains(item.ModuleKey, StringComparer.OrdinalIgnoreCase))
                continue;

            var entity = existing.FirstOrDefault(e => e.ModuleKey.Equals(item.ModuleKey, StringComparison.OrdinalIgnoreCase));
            if (entity == null)
            {
                entity = new ModulePermissionSetting
                {
                    ModuleKey = item.ModuleKey
                };
                _db.ModulePermissionSettings.Add(entity);
                existing.Add(entity);
            }

            entity.ViewRolesCsv = BuildRolesCsv(GetEnabledRoles(item, isView: true));
            entity.ModifyRolesCsv = BuildRolesCsv(GetEnabledRoles(item, isView: false));
            entity.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
        _cache.Remove(PermissionsCacheKey);
    }

    public async Task SaveBellNotificationSettingsAsync(bool enabled, string? soundOption)
    {
        var pref = await GetOrCreatePreferenceAsync(trackChanges: true);
        pref.BellNotificationSoundEnabled = enabled;
        pref.BellNotificationSoundOption = NormalizeSoundOption(soundOption);
        pref.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _cache.Remove(PreferenceCacheKey);
    }

    public async Task SaveProVersionSettingsAsync(ProVersionSettingsViewModel model)
    {
        var pref = await GetOrCreatePreferenceAsync(trackChanges: true);
        pref.FreeProjectLimit = NormalizeLimit(model.FreeProjectLimit, ProVersionDefaults.FreeProjectLimit);
        pref.FreeBugLimit = NormalizeLimit(model.FreeBugLimit, ProVersionDefaults.FreeBugLimit);
        pref.FreeFeatureLimit = NormalizeLimit(model.FreeFeatureLimit, ProVersionDefaults.FreeFeatureLimit);
        pref.EnableAiAutomation = model.EnableAiAutomation;
        pref.AllowAiAutomationForFreePlan =
            model.AllowAiAutomationForFreePlan;
        pref.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _cache.Remove(PreferenceCacheKey);
    }

    public async Task<bool> CanViewModuleAsync(ClaimsPrincipal user, string moduleKey)
    {
        return await HasAccessAsync(user, moduleKey, isModify: false);
    }

    public async Task<bool> CanModifyModuleAsync(ClaimsPrincipal user, string moduleKey)
    {
        return await HasAccessAsync(user, moduleKey, isModify: true);
    }

    public async Task<RuntimeSystemAccessViewModel> BuildRuntimeAccessAsync(ClaimsPrincipal user)
    {
        var byKey = await GetCachedPermissionsAsync();
        var pref = await GetOrCreatePreferenceAsync(trackChanges: false);

        return new RuntimeSystemAccessViewModel
        {
            CanViewAllProjects = IsAllowed(user, byKey.GetValueOrDefault(AppModuleKeys.AllProjects), isModify: false),
            CanModifyAllProjects = IsAllowed(user, byKey.GetValueOrDefault(AppModuleKeys.AllProjects), isModify: true),
            CanViewFeatures = IsAllowed(user, byKey.GetValueOrDefault(AppModuleKeys.Features), isModify: false),
            CanModifyFeatures = IsAllowed(user, byKey.GetValueOrDefault(AppModuleKeys.Features), isModify: true),
            CanViewBugs = IsAllowed(user, byKey.GetValueOrDefault(AppModuleKeys.Bugs), isModify: false),
            CanModifyBugs = IsAllowed(user, byKey.GetValueOrDefault(AppModuleKeys.Bugs), isModify: true),
            CanViewQaTesting = IsAllowed(user, byKey.GetValueOrDefault(AppModuleKeys.QaTesting), isModify: false),
            CanModifyQaTesting = IsAllowed(user, byKey.GetValueOrDefault(AppModuleKeys.QaTesting), isModify: true),
            BellNotificationSoundEnabled = pref.BellNotificationSoundEnabled,
            BellNotificationSoundOption = NormalizeSoundOption(pref.BellNotificationSoundOption)
        };
    }

    private async Task<bool> HasAccessAsync(ClaimsPrincipal user, string moduleKey, bool isModify)
    {
        if (user?.Identity?.IsAuthenticated != true)
            return false;

        if (!AppModuleKeys.All.Contains(moduleKey, StringComparer.OrdinalIgnoreCase))
            return false;

        var permissions = await GetCachedPermissionsAsync();
        var permission = permissions.GetValueOrDefault(moduleKey);

        return IsAllowed(user, permission, isModify);
    }

    private async Task<Dictionary<string, ModulePermissionSetting>> GetCachedPermissionsAsync()
    {
        return (await _cache.GetOrCreateAsync(PermissionsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var list = await _db.ModulePermissionSettings.AsNoTracking().ToListAsync();
            return list.ToDictionary(p => p.ModuleKey, StringComparer.OrdinalIgnoreCase);
        }))!;
    }

    private static bool IsAllowed(ClaimsPrincipal user, ModulePermissionSetting? permission, bool isModify)
    {
        if (permission == null)
            return false;

        var allowedRoles = ParseRolesCsv(isModify ? permission.ModifyRolesCsv : permission.ViewRolesCsv);
        return allowedRoles.Any(user.IsInRole);
    }

    private static ModulePermissionEditItemViewModel BuildPermissionEditor(string moduleKey, ModulePermissionSetting? permission)
    {
        var (defaultView, defaultModify) = GetDefaultRoles(moduleKey);
        var viewRoles = permission is null ? defaultView : ParseRolesCsv(permission.ViewRolesCsv);
        var modifyRoles = permission is null ? defaultModify : ParseRolesCsv(permission.ModifyRolesCsv);

        return new ModulePermissionEditItemViewModel
        {
            ModuleKey = moduleKey,
            DisplayName = GetDisplayName(moduleKey),

            CanViewAdmin = viewRoles.Contains("Admin"),
            CanViewTester = viewRoles.Contains("Tester"),
            CanViewDeveloper = viewRoles.Contains("Developer"),
            CanViewAgent = viewRoles.Contains("Agent"),
            CanViewEmployee = viewRoles.Contains("Employee"),

            CanModifyAdmin = modifyRoles.Contains("Admin"),
            CanModifyTester = modifyRoles.Contains("Tester"),
            CanModifyDeveloper = modifyRoles.Contains("Developer"),
            CanModifyAgent = modifyRoles.Contains("Agent"),
            CanModifyEmployee = modifyRoles.Contains("Employee")
        };
    }

    private async Task<SystemPreference> GetOrCreatePreferenceAsync(bool trackChanges)
    {
        if (!trackChanges)
        {
            return (await _cache.GetOrCreateAsync(PreferenceCacheKey, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await _db.SystemPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.Id == 1)
                       ?? new SystemPreference();
            }))!;
        }

        var pref = await _db.SystemPreferences.FirstOrDefaultAsync(p => p.Id == 1);
        if (pref != null)
            return pref;

        var created = new SystemPreference
        {
            Id = 1,
            BellNotificationSoundEnabled = true,
            BellNotificationSoundOption = BellSoundOptions.Classic,
            FreeProjectLimit = ProVersionDefaults.FreeProjectLimit,
            FreeBugLimit = ProVersionDefaults.FreeBugLimit,
            FreeFeatureLimit = ProVersionDefaults.FreeFeatureLimit,
            EnableAiAutomation = true,
            AllowAiAutomationForFreePlan = false,
            UpdatedAt = DateTime.UtcNow
        };

        _db.SystemPreferences.Add(created);
        await _db.SaveChangesAsync();
        return created;
    }

    private static HashSet<string> ParseRolesCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return [];

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(role => RoleOrder.Contains(role, StringComparer.OrdinalIgnoreCase))
            .Select(role => RoleOrder.First(r => r.Equals(role, StringComparison.OrdinalIgnoreCase)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyCollection<string> GetEnabledRoles(ModulePermissionEditItemViewModel item, bool isView)
    {
        var selected = new List<string>();

        if (isView ? item.CanViewAdmin : item.CanModifyAdmin) selected.Add("Admin");
        if (isView ? item.CanViewTester : item.CanModifyTester) selected.Add("Tester");
        if (isView ? item.CanViewDeveloper : item.CanModifyDeveloper) selected.Add("Developer");
        if (isView ? item.CanViewAgent : item.CanModifyAgent) selected.Add("Agent");
        if (isView ? item.CanViewEmployee : item.CanModifyEmployee) selected.Add("Employee");

        return selected;
    }

    private static string BuildRolesCsv(IEnumerable<string> roles)
    {
        var ordered = RoleOrder.Where(role => roles.Contains(role, StringComparer.OrdinalIgnoreCase));
        return string.Join(",", ordered);
    }

    private static string NormalizeActiveMenu(string? activeMenu)
    {
        if (string.Equals(activeMenu, "BellNotification", StringComparison.OrdinalIgnoreCase))
            return "BellNotification";
        if (string.Equals(activeMenu, "ProVersion", StringComparison.OrdinalIgnoreCase))
            return "ProVersion";
        return "ModulePermission";
    }

    private static ProVersionSettingsViewModel BuildProVersionSettings(SystemPreference pref)
    {
        return new ProVersionSettingsViewModel
        {
            FreeProjectLimit = NormalizeLimit(pref.FreeProjectLimit, ProVersionDefaults.FreeProjectLimit),
            FreeBugLimit = NormalizeLimit(pref.FreeBugLimit, ProVersionDefaults.FreeBugLimit),
            FreeFeatureLimit = NormalizeLimit(pref.FreeFeatureLimit, ProVersionDefaults.FreeFeatureLimit),
            EnableAiAutomation = pref.EnableAiAutomation,
            AllowAiAutomationForFreePlan =
                pref.AllowAiAutomationForFreePlan
        };
    }

    private static string NormalizeSoundOption(string? soundOption)
    {
        if (string.IsNullOrWhiteSpace(soundOption))
            return BellSoundOptions.Classic;

        return BellSoundOptions.All.Contains(soundOption, StringComparer.OrdinalIgnoreCase)
            ? BellSoundOptions.All.First(option => option.Equals(soundOption, StringComparison.OrdinalIgnoreCase))
            : BellSoundOptions.Classic;
    }

    private static int NormalizeLimit(int value, int fallback)
    {
        return value < 0 ? fallback : value;
    }

    private static string GetDisplayName(string moduleKey)
    {
        return moduleKey switch
        {
            AppModuleKeys.AllProjects => "All Projects",
            AppModuleKeys.Features => "Features",
            AppModuleKeys.Bugs => "Bugs",
            AppModuleKeys.QaTesting => "QA Testing",
            _ => moduleKey
        };
    }

    private static (HashSet<string> View, HashSet<string> Modify) GetDefaultRoles(string moduleKey)
    {
        return moduleKey switch
        {
            AppModuleKeys.AllProjects => (
                new HashSet<string>(["Admin", "Tester", "Developer", "Agent", "Employee"], StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(["Admin"], StringComparer.OrdinalIgnoreCase)),
            AppModuleKeys.Features => (
                new HashSet<string>(["Admin", "Tester", "Developer", "Agent", "Employee"], StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(["Admin", "Developer"], StringComparer.OrdinalIgnoreCase)),
            AppModuleKeys.Bugs => (
                new HashSet<string>(["Admin", "Tester", "Developer", "Agent"], StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(["Admin", "Tester", "Developer", "Agent"], StringComparer.OrdinalIgnoreCase)),
            AppModuleKeys.QaTesting => (
                new HashSet<string>(["Admin", "Tester", "Developer"], StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(["Admin", "Tester"], StringComparer.OrdinalIgnoreCase)),
            _ => ([], [])
        };
    }
}
