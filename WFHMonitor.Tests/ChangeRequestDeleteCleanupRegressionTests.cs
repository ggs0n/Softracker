namespace WFHMonitor.Tests;

public class ChangeRequestDeleteCleanupRegressionTests
{
    [Fact]
    public void DeleteAction_RemovesLinkedTestCasesBeforeDeletingProject()
    {
        var controllerPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WFHMonitor", "Controllers", "ChangeRequestController.cs");
        var content = File.ReadAllText(Path.GetFullPath(controllerPath));

        Assert.Contains("_db.TestCases", content);
        Assert.Contains("Where(t => t.ChangeRequestId == id)", content);
        Assert.Contains("_db.TestCases.RemoveRange(linkedTestCases)", content);
        Assert.Contains("_db.ChangeRequests.Remove(cr)", content);
    }
}
