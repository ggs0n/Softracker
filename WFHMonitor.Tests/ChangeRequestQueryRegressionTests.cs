namespace WFHMonitor.Tests;

public class ChangeRequestQueryRegressionTests
{
    [Fact]
    public void ChangeRequestController_UsesSplitQueriesOnHeavyMultiIncludeScreens()
    {
        var controllerPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WFHMonitor", "Controllers", "ChangeRequestController.cs");
        var content = File.ReadAllText(Path.GetFullPath(controllerPath));

        Assert.Contains(".Include(c => c.Pics).ThenInclude(p => p.Employee)\r\n            .Include(c => c.Features)\r\n            .AsSplitQuery()", content);
        Assert.Contains(".Include(c => c.Features.OrderBy(f => f.Name))\r\n                .ThenInclude(f => f.AssignedDeveloper)\r\n            .AsSplitQuery()", content);
        Assert.Contains(".Include(c => c.RepositoryFeatures.OrderBy(f => f.Name))\r\n            .AsSplitQuery()", content);
    }
}
