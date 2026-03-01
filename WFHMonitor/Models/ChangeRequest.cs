using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WFHMonitor.Models;

public enum CrStatus
{
    Draft,
    InReview,
    Approved,
    InProgress,
    Done,
    Rejected
}

public enum CrPriority
{
    Low,
    Medium,
    High,
    Critical
}

public enum CrStage
{
    ProjectStart,
    Development,
    Testing,
    Deploy,
    DeploymentComplete
}

public class ChangeRequest
{
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string CrNumber { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    public CrStatus Status { get; set; } = CrStatus.Draft;

    public CrPriority Priority { get; set; } = CrPriority.Medium;

    public CrStage Stage { get; set; } = CrStage.ProjectStart;

    [MaxLength(500)]
    [Display(Name = "Figma Link")]
    public string? FigmaLink { get; set; }

    [MaxLength(500)]
    [Display(Name = "Solution Architect Spec / Diagram")]
    public string? ArchSpecLink { get; set; }

    [MaxLength(2000)]
    [Display(Name = "Architect Spec Preview Notes")]
    public string? ArchSpecNotes { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Timeline Start")]
    public DateTime? TimelineStart { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Timeline End")]
    public DateTime? TimelineEnd { get; set; }

    [MaxLength(100)]
    [Display(Name = "GitHub Repo Owner")]
    public string? GitHubRepoOwner { get; set; }

    [MaxLength(100)]
    [Display(Name = "GitHub Repo Name")]
    public string? GitHubRepoName { get; set; }

    [MaxLength(500)]
    [Display(Name = "GitHub Repo URL")]
    public string? GitHubRepoUrl { get; set; }

    [MaxLength(100)]
    [Display(Name = "GitHub Branch")]
    public string? GitHubBranch { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(CreatedBy))]
    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }

    public ICollection<ChangeRequestPic> Pics { get; set; } = new List<ChangeRequestPic>();
    public ICollection<ArchSpecImage> ArchSpecImages { get; set; } = new List<ArchSpecImage>();
    public ICollection<ChangeRequestDocument> Documents { get; set; } = new List<ChangeRequestDocument>();
}

public class ArchSpecImage
{
    public int Id { get; set; }

    [ForeignKey(nameof(ChangeRequest))]
    public int ChangeRequestId { get; set; }
    public ChangeRequest? ChangeRequest { get; set; }

    [Required, MaxLength(300)]
    public string FileName { get; set; } = string.Empty;   // stored file name on disk

    [MaxLength(1000)]
    public string? Caption { get; set; }

    public int SortOrder { get; set; } = 0;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class ChangeRequestPic
{
    public int Id { get; set; }

    [ForeignKey(nameof(ChangeRequest))]
    public int ChangeRequestId { get; set; }
    public ChangeRequest? ChangeRequest { get; set; }

    [ForeignKey(nameof(Employee))]
    public string EmployeeId { get; set; } = string.Empty;
    public ApplicationUser? Employee { get; set; }

    [MaxLength(100)]
    public string? Role { get; set; }
}

public class ChangeRequestDocument
{
    public int Id { get; set; }

    [ForeignKey(nameof(ChangeRequest))]
    public int ChangeRequestId { get; set; }
    public ChangeRequest? ChangeRequest { get; set; }

    [Required, MaxLength(300)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string OriginalFileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
