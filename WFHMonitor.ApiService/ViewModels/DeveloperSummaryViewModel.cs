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

    public int BugFixedCount { get; set; }
    public int CriticalBugFixedCount { get; set; }
    public int CrDeliveredOnTimeCount { get; set; }
    public int TaskCompletedBeforeDueDateCount { get; set; }
    public int ReopenedBugPenaltyCount { get; set; }
    public int FailedSlaPenaltyCount { get; set; }

    public int BugFixedPoints => BugFixedCount * 20;
    public int CriticalBugFixedPoints => CriticalBugFixedCount * 40;
    public int CrDeliveredOnTimePoints => CrDeliveredOnTimeCount * 30;
    public int TaskCompletedBeforeDueDatePoints => TaskCompletedBeforeDueDateCount * 15;
    public int ReopenedBugPenaltyPoints => ReopenedBugPenaltyCount * 10;
    public int FailedSlaPenaltyPoints => FailedSlaPenaltyCount * 15;

    public int TotalXpPoints =>
        BugFixedPoints
        + CriticalBugFixedPoints
        + CrDeliveredOnTimePoints
        + TaskCompletedBeforeDueDatePoints
        - ReopenedBugPenaltyPoints
        - FailedSlaPenaltyPoints;
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
