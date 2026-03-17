using WFHMonitor.Models;
using System.ComponentModel.DataAnnotations;

namespace WFHMonitor.ViewModels;

public class SettingsPageViewModel
{
    public string ActiveMenu { get; set; } = "ModulePermission";
    public List<ModulePermissionEditItemViewModel> Modules { get; set; } = new();
    public bool BellNotificationSoundEnabled { get; set; }
    public string BellNotificationSoundOption { get; set; } = BellSoundOptions.Classic;
    public ProVersionSettingsViewModel ProVersion { get; set; } = new();
}

public class ModulePermissionEditItemViewModel
{
    public string ModuleKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    public bool CanViewAdmin { get; set; }
    public bool CanViewTester { get; set; }
    public bool CanViewDeveloper { get; set; }
    public bool CanViewAgent { get; set; }
    public bool CanViewEmployee { get; set; }

    public bool CanModifyAdmin { get; set; }
    public bool CanModifyTester { get; set; }
    public bool CanModifyDeveloper { get; set; }
    public bool CanModifyAgent { get; set; }
    public bool CanModifyEmployee { get; set; }
}

public class RuntimeSystemAccessViewModel
{
    public bool CanViewAllProjects { get; set; }
    public bool CanModifyAllProjects { get; set; }
    public bool CanViewFeatures { get; set; }
    public bool CanModifyFeatures { get; set; }
    public bool CanViewBugs { get; set; }
    public bool CanModifyBugs { get; set; }
    public bool BellNotificationSoundEnabled { get; set; }
    public string BellNotificationSoundOption { get; set; } = BellSoundOptions.Classic;
}

public class ProVersionSettingsViewModel
{
    [Range(0, 10000)]
    public int FreeProjectLimit { get; set; } = ProVersionDefaults.FreeProjectLimit;

    [Range(0, 10000)]
    public int FreeBugLimit { get; set; } = ProVersionDefaults.FreeBugLimit;

    [Range(0, 10000)]
    public int FreeFeatureLimit { get; set; } = ProVersionDefaults.FreeFeatureLimit;

    public bool AllowOpenClawForFreePlan { get; set; }
}
