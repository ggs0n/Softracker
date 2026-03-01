namespace WFHMonitor.ViewModels;

public class AgentDashboardViewModel
{
    public List<AgentInfoViewModel> Agents { get; set; } = new();
    public List<AgentWorkItemViewModel> RunningAgentWorkItems { get; set; } = new();
    public List<AgentWorkItemViewModel> AgentWorkItems { get; set; } = new();
    public AgentQueueSnapshotViewModel QueueSnapshot { get; set; } = new();
    public List<AgentActivityItemViewModel> RecentActivities { get; set; } = new();
    public List<CronJobStatusViewModel> RunningCronJobs { get; set; } = new();
    public List<CronJobStatusViewModel> CronJobs { get; set; } = new();
}

public class AgentInfoViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Idle";
    public int RunningWorkItems { get; set; }
    public int QueuedWorkItems { get; set; }
}

public class AgentWorkItemViewModel
{
    public int Id { get; set; }
    public string BugNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string AgentStatus { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class CronJobStatusViewModel
{
    public string Name { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsRunning { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class AgentQueueSnapshotViewModel
{
    public int Total { get; set; }
    public int Queued { get; set; }
    public int InProgress { get; set; }
    public int Blocked { get; set; }
    public int PrRaised { get; set; }
    public int Failed { get; set; }
}

public class AgentActivityItemViewModel
{
    public int BugId { get; set; }
    public string BugNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? StatusTransition { get; set; }
    public DateTime CreatedAt { get; set; }
}
