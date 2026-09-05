using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WFHMonitor.Models;

public class EodReport
{
    public int Id { get; set; }

    [ForeignKey(nameof(Employee))]
    public string EmployeeId { get; set; } = string.Empty;
    public ApplicationUser? Employee { get; set; }

    public DateTime ReportDate { get; set; } = DateTime.UtcNow.Date;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(2000)]
    public string? Blockers { get; set; }

    [MaxLength(2000)]
    public string? TomorrowPlan { get; set; }

    [MaxLength(2000)]
    public string? EvidenceLinks { get; set; }

    public ICollection<EodReportTask> EodReportTasks { get; set; } = new List<EodReportTask>();
}

public enum EodTaskType
{
    Completed,
    InProgress
}

public class EodReportTask
{
    public int Id { get; set; }

    [ForeignKey(nameof(EodReport))]
    public int EodReportId { get; set; }
    public EodReport? EodReport { get; set; }

    [ForeignKey(nameof(Task))]
    public int TaskId { get; set; }
    public WorkTask? Task { get; set; }

    public EodTaskType Type { get; set; }

    [MaxLength(500)]
    public string? NextStep { get; set; }
}
