using System.ComponentModel.DataAnnotations;

namespace WFHMonitor.Models;

public static class AppModuleKeys
{
    public const string AllProjects = "AllProjects";
    public const string Features = "Features";
    public const string Bugs = "Bugs";
    public const string QaTesting = "QaTesting";

    public static readonly string[] All = [AllProjects, Features, Bugs, QaTesting];
}

public class ModulePermissionSetting
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string ModuleKey { get; set; } = string.Empty;

    [Required, MaxLength(400)]
    public string ViewRolesCsv { get; set; } = string.Empty;

    [Required, MaxLength(400)]
    public string ModifyRolesCsv { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SystemPreference
{
    public int Id { get; set; } = 1;
    public bool BellNotificationSoundEnabled { get; set; } = true;
    [Required, MaxLength(30)]
    public string BellNotificationSoundOption { get; set; } = BellSoundOptions.Classic;
    public int FreeProjectLimit { get; set; } = ProVersionDefaults.FreeProjectLimit;
    public int FreeBugLimit { get; set; } = ProVersionDefaults.FreeBugLimit;
    public int FreeFeatureLimit { get; set; } = ProVersionDefaults.FreeFeatureLimit;
    public bool EnableCodexAgents { get; set; } = true;
    public bool AllowCodexForFreePlan { get; set; } = false;
    [Required, MaxLength(100)]
    public string CodexModel { get; set; } = CodexAiDefaults.Model;
    [Required, MaxLength(20)]
    public string CodexReasoningEffort { get; set; } = CodexAiDefaults.ReasoningEffort;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class CodexAiDefaults
{
    public const string Model = "gpt-5.6-sol";
    public const string ReasoningEffort = "low";
    public static readonly string[] ReasoningEfforts = ["none", "minimal", "low", "medium", "high", "xhigh", "max", "ultra"];
}

public static class ProVersionDefaults
{
    public const int FreeProjectLimit = 2;
    public const int FreeBugLimit = 2;
    public const int FreeFeatureLimit = 2;
}

public static class BellSoundOptions
{
    public const string Classic = "classic";
    public const string Soft = "soft";
    public const string Chime = "chime";
    public const string Alert = "alert";

    public static readonly string[] All = [Classic, Soft, Chime, Alert];
}
