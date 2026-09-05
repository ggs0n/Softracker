namespace WFHMonitor.Configuration;

public sealed class AiAutomationServiceSettings
{
    public const string SectionName = "AiAutomationService";

    public string BaseUrl { get; set; } = "http://127.0.0.1:5201";
    public string SharedSecret { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 300;
    public int MaxFindingsPerScan { get; set; } = 8;
}
