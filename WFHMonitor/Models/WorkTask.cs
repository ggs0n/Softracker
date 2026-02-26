using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WFHMonitor.Models;

public enum WorkTaskStatus
{
    ToDo,
    InProgress,
    Blocked,
    Done
}

public class WorkTask
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Details { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.ToDo;

    public DateTime? TimelineStart { get; set; }

    public DateTime? TimelineEnd { get; set; }

    public DateTime? DueDate { get; set; }

    [ForeignKey(nameof(ChangeRequest))]
    public int? ChangeRequestId { get; set; }
    public ChangeRequest? ChangeRequest { get; set; }

    [ForeignKey(nameof(BugReport))]
    public int? BugReportId { get; set; }
    public BugReport? BugReport { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(Assignee))]
    public string? AssigneeId { get; set; }
    public ApplicationUser? Assignee { get; set; }

    [ForeignKey(nameof(CreatedBy))]
    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }
}
