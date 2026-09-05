namespace WFHMonitor.Tests;

public class CodeReadinessPaginationTests
{
    [Fact]
    public void FixFirst_UsesTenFindingsPerPage()
    {
        var viewPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "WFHMonitor", "Views", "ChangeRequest", "CodeReadiness.cshtml"));
        var content = File.ReadAllText(viewPath);

        Assert.Contains("data-fix-first-row", content);
        Assert.Contains("id=\"fixFirstPagination\"", content);
        Assert.Contains("const pageSize = 10;", content);
        Assert.Contains("Go to previous page", content);
        Assert.Contains("Go to next page", content);
    }
}
