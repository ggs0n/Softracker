namespace WFHMonitor.Configuration;

public sealed class GitAutomationSettings
{
    public const string SectionName = "GitAutomation";

    public string WorkspaceRoot { get; set; } =
        "../.wfhtmp/ai-workspaces";
    public string GitExecutablePath { get; set; } = "git";
    public string CloneUrlTemplate { get; set; } =
        "https://github.com/{owner}/{repository}.git";
    public string BranchPrefix { get; set; } = "wfhmonitor/fix";
    public string CommitUserName { get; set; } =
        "WFHMonitor AI Automation";
    public string CommitUserEmail { get; set; } =
        "wfhmonitor-ai@localhost";
    public int CommandTimeoutSeconds { get; set; } = 300;
    public int WorkspaceRetentionHours { get; set; } = 24;
    public int CleanupIntervalMinutes { get; set; } = 30;
}
