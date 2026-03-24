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
}
