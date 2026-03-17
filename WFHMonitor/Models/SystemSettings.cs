using System.ComponentModel.DataAnnotations;

namespace WFHMonitor.Models;

public static class AppModuleKeys
{
    public const string AllProjects = "AllProjects";
    public const string Features = "Features";
    public const string Bugs = "Bugs";

    public static readonly string[] All = [AllProjects, Features, Bugs];
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
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class BellSoundOptions
{
    public const string Classic = "classic";
    public const string Soft = "soft";
    public const string Chime = "chime";
    public const string Alert = "alert";

    public static readonly string[] All = [Classic, Soft, Chime, Alert];
}
