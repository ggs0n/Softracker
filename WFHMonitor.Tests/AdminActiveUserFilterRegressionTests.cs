namespace WFHMonitor.Tests;

public class AdminActiveUserFilterRegressionTests
{
    [Fact]
    public void AdminController_FiltersInactiveUsersFromDashboardAndEmployees()
    {
        var controllerPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WFHMonitor", "Controllers", "AdminController.cs");
        var content = File.ReadAllText(Path.GetFullPath(controllerPath));

        Assert.Contains(".Where(u => u.IsActive)", content);
        Assert.Contains(".Where(user => user.IsActive)", content);
    }
}
