using System.ComponentModel.DataAnnotations;
using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class EodReportCreateViewModel
{
    [Display(Name = "Completed Tasks")]
    public List<int> CompletedTaskIds { get; set; } = new();

    [Display(Name = "In Progress Tasks")]
    public List<InProgressTaskItem> InProgressTasks { get; set; } = new();

    [MaxLength(2000)]
    public string? Blockers { get; set; }

    [Required, MaxLength(2000)]
    [Display(Name = "Tomorrow's Plan")]
    public string TomorrowPlan { get; set; } = string.Empty;

    [MaxLength(2000)]
    [Display(Name = "Evidence Links (PR / Doc / Screenshot)")]
    public string? EvidenceLinks { get; set; }

    // Populated in controller for the view
    public List<WorkTask> AvailableTasks { get; set; } = new();
}

public class InProgressTaskItem
{
    public int TaskId { get; set; }
    public bool Selected { get; set; }

    [MaxLength(500)]
    public string? NextStep { get; set; }
}

public class EodReportDetailsViewModel
{
    public EodReport Report { get; set; } = null!;
    public List<EodReportTask> CompletedItems { get; set; } = new();
    public List<EodReportTask> InProgressItems { get; set; } = new();
}
