namespace WFHMonitor.Configuration;

public sealed class AiAutomationSettings
{
    public const string SectionName = "AiAutomation";

    public string ProfileId { get; set; } = "codex";
    public List<string> AllowedProfileIds { get; set; } = ["codex"];
    public string ScreenshotImportDirectory { get; set; } = string.Empty;
    public List<string> ScreenshotImportDirectories { get; set; } = [];
    public int ScreenshotImportLookbackMinutes { get; set; } = 180;
    public int ScreenshotImportMaxFiles { get; set; } = 5;
}
