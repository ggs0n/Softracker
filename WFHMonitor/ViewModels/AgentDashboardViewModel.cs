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
    /// <summary>Slug used as the photo filename key (e.g. "main", "worker1").</summary>
    public string ProfileFolder { get; set; } = string.Empty;
    /// <summary>Relative URL to the uploaded photo, or null if none.</summary>
    public string? PhotoUrl { get; set; }

    // Performance rating (0–100) based on completed vs failed tasks
    public int TotalAssigned { get; set; }
    public int Completed { get; set; }   // PrRaised = done
    public int Failed { get; set; }
    public int Blocked { get; set; }

    /// <summary>0–100 rating. -1 means no data yet.</summary>
    public int PerformanceScore { get; set; } = -1;

    /// <summary>Letter grade derived from PerformanceScore.</summary>
    public string PerformanceGrade => PerformanceScore switch
    {
        < 0 => "N/A",
        >= 90 => "S",
        >= 80 => "A",
        >= 65 => "B",
        >= 50 => "C",
        >= 30 => "D",
        _ => "F"
    };

    public string PerformanceGradeColor => PerformanceGrade switch
    {
        "S" => "#f59e0b",
        "A" => "#10b981",
        "B" => "#6366f1",
        "C" => "#0ea5e9",
        "D" => "#eab308",
        "F" => "#ef4444",
        _   => "#64748b"
    };
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
