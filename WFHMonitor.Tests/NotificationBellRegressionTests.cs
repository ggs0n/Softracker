namespace WFHMonitor.Tests;

public class NotificationBellRegressionTests
{
    [Fact]
    public void Layout_ReinitializesBellDropdownAfterRefresh()
    {
        var layoutPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WFHMonitor", "Views", "Shared", "_Layout.cshtml");
        var content = File.ReadAllText(Path.GetFullPath(layoutPath));

        Assert.Contains("window.bootstrap?.Dropdown?.getOrCreateInstance(bellButton)", content);
        Assert.Contains("dropdown.dispose();", content);
        Assert.Contains("dropdown.hide();", content);
    }

    [Fact]
    public void ProjectDetails_ContainsCodeReadinessTab()
    {
        var detailsPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WFHMonitor", "Views", "ChangeRequest", "Details.cshtml");
        var content = File.ReadAllText(Path.GetFullPath(detailsPath));

        Assert.Contains("asp-action=\"CodeReadiness\"", content);
        Assert.Contains("Code Readiness", content);
    }

    [Fact]
    public void CodeReadinessView_ShowsProjectDrivenSections()
    {
        var viewPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WFHMonitor", "Views", "ChangeRequest", "CodeReadiness.cshtml");
        var content = File.ReadAllText(Path.GetFullPath(viewPath));

        Assert.Contains("Repository Readiness", content);
        Assert.Contains("Fix First", content);
        Assert.Contains("SOLID Review", content);
        Assert.Contains("Architecture Flow", content);
        Assert.Contains("Incomplete Work", content);
        Assert.Contains("Related Modules", content);
        Assert.Contains("Design Patterns Used", content);
        Assert.Contains("OWASP Top 10:2025 Analysis", content);
    }

    [Fact]
    public void GitHubOAuthCallback_MatchesDesktopHttpOrigin()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var settings = File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "appsettings.example.json"));
        var desktop = File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor.Desktop", "MainWindow.xaml.cs"));

        Assert.Contains("\"RedirectUri\": \"http://localhost:5227/GitHubAuth/Callback\"", settings);
        Assert.Contains("new Uri(\"http://localhost:5227\")", desktop);
        Assert.DoesNotContain("\"RedirectUri\": \"https://localhost:5227/GitHubAuth/Callback\"", settings);
    }
}
