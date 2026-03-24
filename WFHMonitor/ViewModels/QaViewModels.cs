using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class QaIndexViewModel
{
    public List<TestCase> TestCases { get; set; } = new();
    public List<ChangeRequest> Projects { get; set; } = new();
    public List<BugReport> LinkedBugs { get; set; } = new();
    public Dictionary<int, int> RelatedBugCounts { get; set; } = new();

    // Summary stats
    public int TotalCount { get; set; }
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int PendingCount { get; set; }
    public int SkipCount { get; set; }
    public double PassRate => TotalCount == 0 ? 0 : Math.Round(PassedCount * 100.0 / TotalCount, 0);

    // Coverage by module
    public List<ModuleCoverage> ModuleCoverages { get; set; } = new();

    // Filters
    public string? FilterCategory { get; set; }
    public string? FilterStatus { get; set; }
    public int? FilterProjectId { get; set; }
}

public class QaTestCaseDetailsViewModel
{
    public TestCase TestCase { get; set; } = new();
    public List<BugReport> RelatedBugs { get; set; } = new();
}

public class ModuleCoverage
{
    public string Module { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Passed { get; set; }
    public int CoveragePercent => Total == 0 ? 0 : (int)Math.Round(Passed * 100.0 / Total);
}

public class TestCaseFormViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Module { get; set; }
    public TestCaseStatus Status { get; set; } = TestCaseStatus.Pending;
    public TestCaseCategory Category { get; set; } = TestCaseCategory.All;
    public TestCaseEnvironment Environment { get; set; } = TestCaseEnvironment.Dev;
    public int? ChangeRequestId { get; set; }
    public int? LinkedBugId { get; set; }
    public List<SelectOptionItem> ProjectOptions { get; set; } = new();
    public List<SelectOptionItem> BugOptions { get; set; } = new();
}

public class SelectOptionItem
{
    public string Value { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
