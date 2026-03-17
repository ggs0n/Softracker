using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WFHMonitor.Models;

public enum BugStatus
{
    New,
    Testing,
    Complete
}

public enum BugAssigneeType
{
    Developer,
    Agent
}

public enum BugAgentStatus
{
    None,
    Queued,
    InProgress,
    Blocked,
    PrRaised,
    Failed
}

public class BugReport
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string BugNumber { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(4000)]
    public string? Workflow { get; set; }

    [MaxLength(4000)]
    public string? StepsToReproduce { get; set; }

    [MaxLength(200)]
    public string? ModuleImpacted { get; set; }

    [MaxLength(500)]
    public string? PullRequestUrl { get; set; }

    public BugStatus Status { get; set; } = BugStatus.New;
    public BugAssigneeType AssigneeType { get; set; } = BugAssigneeType.Developer;
    public BugAgentStatus AgentStatus { get; set; } = BugAgentStatus.None;

    [ForeignKey(nameof(ChangeRequest))]
    public int? ChangeRequestId { get; set; }
    public ChangeRequest? ChangeRequest { get; set; }

    [MaxLength(300)]
    public string? ChangeRequestReferenceText { get; set; }

    [ForeignKey(nameof(AssignedDeveloper))]
    public string? AssignedDeveloperId { get; set; }
    public ApplicationUser? AssignedDeveloper { get; set; }

    [ForeignKey(nameof(CreatedBy))]
    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BugScreenshot> Screenshots { get; set; } = new List<BugScreenshot>();
    public ICollection<BugDocument> Documents { get; set; } = new List<BugDocument>();
    public ICollection<BugActivity> Activities { get; set; } = new List<BugActivity>();
}

public class BugScreenshot
{
    public int Id { get; set; }

    [ForeignKey(nameof(BugReport))]
    public int BugReportId { get; set; }
    public BugReport? BugReport { get; set; }

    [Required, MaxLength(300)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string OriginalFileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class BugDocument
{
    public int Id { get; set; }

    [ForeignKey(nameof(BugReport))]
    public int BugReportId { get; set; }
    public BugReport? BugReport { get; set; }

    [Required, MaxLength(300)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string OriginalFileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class BugActivity
{
    public int Id { get; set; }

    [ForeignKey(nameof(BugReport))]
    public int BugReportId { get; set; }
    public BugReport? BugReport { get; set; }

    [Required, MaxLength(200)]
    public string Action { get; set; } = string.Empty;

    public BugStatus? OldStatus { get; set; }
    public BugStatus? NewStatus { get; set; }

    [ForeignKey(nameof(OldAssignedDeveloper))]
    public string? OldAssignedDeveloperId { get; set; }
    public ApplicationUser? OldAssignedDeveloper { get; set; }

    [ForeignKey(nameof(NewAssignedDeveloper))]
    public string? NewAssignedDeveloperId { get; set; }
    public ApplicationUser? NewAssignedDeveloper { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
