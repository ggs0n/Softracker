namespace WFHMonitor.Tests;

public class CodexSettingsIsolationTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void CodexRuns_IgnoreUserConfigAndPassSoftrackerPreferences()
    {
        var service = File.ReadAllText(Path.Combine(
            RepositoryRoot, "WFHMonitor", "Services", "CodexAgentService.cs"));

        Assert.Contains("\"--ignore-user-config\"", service);
        Assert.Contains("model_reasoning_effort=", service);
        Assert.Contains("GetCodexAiSettingsAsync", service);
    }

    [Fact]
    public void SettingsPage_ProvidesModelReasoningAndCatalogRefresh()
    {
        var view = File.ReadAllText(Path.Combine(
            RepositoryRoot, "WFHMonitor", "Views", "Settings", "Index.cshtml"));
        var preference = File.ReadAllText(Path.Combine(
            RepositoryRoot, "WFHMonitor", "Models", "SystemSettings.cs"));

        Assert.Contains("SaveCodexAi", view);
        Assert.Contains("RefreshCodexModels", view);
        Assert.Contains("CodexAi.Model", view);
        Assert.Contains("CodexAi.ReasoningEffort", view);
        Assert.Contains("CodexModel", preference);
        Assert.Contains("CodexReasoningEffort", preference);
    }
}
