namespace WFHMonitor.AutomationService.Configuration;

public sealed class CodexSettings
{
    public const string SectionName = "Codex";

    public string ExecutablePath { get; set; } = "codex";
    public string Model { get; set; } = "gpt-5.6-sol";
    public string ReasoningEffort { get; set; } = "medium";
    public int StartupTimeoutSeconds { get; set; } = 20;
    public int RequestTimeoutSeconds { get; set; } = 180;
    public int LoginTimeoutSeconds { get; set; } = 300;
    public string WorkingDirectory { get; set; } = string.Empty;
}

public sealed class AiAutomationSettings
{
    public const string SectionName = "AiAutomation";

    public int MaxFindingsPerScan { get; set; } = 8;
    public int MaxModuleImages { get; set; } = 10;
    public int MaxGeneratedImageBytes { get; set; } = 12_000_000;
    public int ImageGenerationTimeoutSeconds { get; set; } = 900;
    public string WorkspaceRoot { get; set; } = "../.wfhtmp/ai-workspaces";
    public string PromptDirectory { get; set; } = "Prompts";
    public string SchemaDirectory { get; set; } = "Schemas";
}
