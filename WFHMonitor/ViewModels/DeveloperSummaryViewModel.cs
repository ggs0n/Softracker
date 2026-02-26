using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class DeveloperSummaryViewModel
{
    public int AssignedBugCount { get; set; }
    public int AssignedChangeRequestCount { get; set; }
    public int LinkedBranchCount { get; set; }
    public int RecentCommitCount { get; set; }
    public string DeveloperEmail { get; set; } = string.Empty;
    public string? GitHubError { get; set; }
    public List<BugReport> AssignedBugs { get; set; } = new();
    public List<ChangeRequest> AssignedChangeRequests { get; set; } = new();
    public List<DeveloperGitHubCommitItem> RecentCommits { get; set; } = new();
    public List<DeveloperGitHubBranchItem> Branches { get; set; } = new();
}

public class DeveloperGitHubCommitItem
{
    public string CrNumber { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Sha { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Url { get; set; } = string.Empty;
}

public class DeveloperGitHubBranchItem
{
    public string CrNumber { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
}
