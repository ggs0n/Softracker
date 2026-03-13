using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class AdminDashboardViewModel
{
    public DateTime Today { get; set; } = DateTime.UtcNow.Date;
    public List<WorkTask> TasksDoneToday { get; set; } = new();
    public List<WorkTask> BlockedTasks { get; set; } = new();
    public int TotalEmployees { get; set; }
    public int TasksDoneTodayCount => TasksDoneToday.Count;

    // Project overview
    public List<ProjectOverviewItem> Projects { get; set; } = new();

    // Bug summary
    public int TotalBugs { get; set; }
    public int OpenBugs { get; set; }
    public int CompleteBugs { get; set; }
}

public class ProjectOverviewItem
{
    public int Id { get; set; }
    public string CrNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public CrStatus Status { get; set; }
    public CrStage Stage { get; set; }
    public CrPriority Priority { get; set; }
    public DateTime? TimelineStart { get; set; }
    public DateTime? TimelineEnd { get; set; }

    // Task breakdown for this project
    public int TotalTasks { get; set; }
    public int DoneTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int BlockedTasks { get; set; }

    // Bug breakdown for this project
    public int TotalBugs { get; set; }
    public int OpenBugs { get; set; }
    public int CompleteBugs { get; set; }

    // Feature breakdown for this project
    public int TotalFeatures { get; set; }
    public int CompletedFeatures { get; set; }

    public int TaskCompletionPercent => TotalTasks == 0 ? 0 : (int)Math.Round(DoneTasks * 100.0 / TotalTasks);
    public int BugResolutionPercent => TotalBugs == 0 ? 100 : (int)Math.Round(CompleteBugs * 100.0 / TotalBugs);
    public int FeatureCompletionPercent => TotalFeatures == 0 ? 0 : (int)Math.Round(CompletedFeatures * 100.0 / TotalFeatures);

    public bool IsOverdue => TimelineEnd.HasValue && TimelineEnd.Value.Date < DateTime.UtcNow.Date && Status != CrStatus.Done;
    public int? DaysRemaining => TimelineEnd.HasValue ? (int)(TimelineEnd.Value.Date - DateTime.UtcNow.Date).TotalDays : null;
}
