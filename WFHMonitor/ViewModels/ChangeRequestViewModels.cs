using System.ComponentModel.DataAnnotations;
using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class ChangeRequestFormViewModel
{
    public int Id { get; set; }

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
    [Display(Name = "SA Spec / Diagram Link")]
    public string? ArchSpecLink { get; set; }

    [MaxLength(2000)]
    [Display(Name = "Architect Spec Notes / Preview")]
    public string? ArchSpecNotes { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime? TimelineStart { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime? TimelineEnd { get; set; }

    [MaxLength(100)]
    [Display(Name = "GitHub Repo Owner")]
    public string? GitHubRepoOwner { get; set; }

    [MaxLength(100)]
    [Display(Name = "GitHub Repo Name")]
    public string? GitHubRepoName { get; set; }

    [MaxLength(500)]
    [Display(Name = "GitHub Repo URL")]
    [Url(ErrorMessage = "Please enter a valid GitHub repository URL.")]
    public string? GitHubRepoUrl { get; set; }

    [MaxLength(100)]
    [Display(Name = "GitHub Branch")]
    public string? GitHubBranch { get; set; }

    [MaxLength(800)]
    [Display(Name = "Technology / Languages")]
    public string? TechnologyStack { get; set; }

    [Display(Name = "Person in Charge (PIC)")]
    public List<PicEntry> Pics { get; set; } = new();

    [Display(Name = "Imported Features")]
    public List<ImportedFeatureEntry> ImportedFeatures { get; set; } = new();

    // for populating dropdown
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> EmployeeOptions { get; set; } = new();
}

public class PicEntry
{
    public string EmployeeId { get; set; } = string.Empty;
    [MaxLength(100)]
    public string? Role { get; set; }
}

public class ImportedFeatureEntry
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsAutoDetected { get; set; } = true;
}
