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
    public CrStage Stage { get; set; } = CrStage.Development;

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

    [Display(Name = "Repository Scan Features")]
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

public class CreateProjectFeatureViewModel
{
    public int? FeatureId { get; set; }

    [Display(Name = "Related Project")]
    [Required]
    public int ChangeRequestId { get; set; }

    [Display(Name = "Feature / Change Title")]
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Affected Module")]
    [MaxLength(200)]
    public string? ModuleImpacted { get; set; }

    [Display(Name = "Linked Bugs")]
    [MaxLength(1000)]
    public string? LinkedBugs { get; set; }

    [Display(Name = "PR Link")]
    [MaxLength(500)]
    [Url(ErrorMessage = "Please enter a valid PR URL.")]
    public string? PullRequestUrl { get; set; }

    [Display(Name = "Status")]
    public CrStatus Status { get; set; } = CrStatus.Draft;

    [Display(Name = "Priority")]
    public CrPriority Priority { get; set; } = CrPriority.Medium;

    [Display(Name = "Stage")]
    public CrStage Stage { get; set; } = CrStage.Development;

    [DataType(DataType.Date)]
    [Display(Name = "Start Date")]
    public DateTime? TimelineStart { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "End Date")]
    public DateTime? TimelineEnd { get; set; }

    [Display(Name = "Assigned To (Developer / Agent)")]
    public string? AssignedDeveloperId { get; set; }

    public string? ReturnUrl { get; set; }

    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> ProjectOptions { get; set; } = new();
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> DeveloperOptions { get; set; } = new();
}

public class ProjectHealthViewModel
{
    public int Score { get; set; }
    public string Label { get; set; } = "Unknown";
    public string Summary { get; set; } = string.Empty;
    public string Complexity { get; set; } = "Medium";
    public List<string> Factors { get; set; } = new();
    public DateTime? AnalyzedAtUtc { get; set; }
    public bool UsedCodex { get; set; }
    public string? Error { get; set; }
}

public class CodeReadinessViewModel
{
    public ChangeRequest Project { get; set; } = new();
    public string RepositoryName { get; set; } = "Not connected";
    public string Branch { get; set; } = "-";
    public string? CommitSha { get; set; }
    public string? RepositoryUrl { get; set; }
    public bool RepositoryConnected { get; set; }
    public int Score { get; set; }
    public string Label { get; set; } = "Not Scanned";
    public string Summary { get; set; } = string.Empty;
    public DateTime ScannedAtUtc { get; set; }
    public string? ScanError { get; set; }
    public CodeReadinessScanStatus DeepScanStatus { get; set; }
    public string DeepScanStatusText { get; set; } = "Not started";
    public string? DeepScanMessage { get; set; }
    public DateTime? DeepScanCompletedAtUtc { get; set; }
    public bool UsedCodexSourceScan { get; set; }
    public int CodexAnalyzedFileCount { get; set; }
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> CodexAgentOptions { get; set; } = [];
    public int SourceFileCount { get; set; }
    public int ModuleCount { get; set; }
    public int DependencyCount { get; set; }
    public int OpenBugCount { get; set; }
    public int FailedTestCount { get; set; }
    public int PendingTestCount { get; set; }
    public int IncompleteFeatureCount { get; set; }
    public List<CodeReadinessCategoryViewModel> Categories { get; set; } = [];
    public List<CodeReadinessFindingViewModel> Findings { get; set; } = [];
    public List<CodeReadinessLayerViewModel> ArchitectureLayers { get; set; } = [];
    public List<CodeReadinessSolidCheckViewModel> SolidChecks { get; set; } = [];
    public List<CodeReadinessDesignPatternViewModel> DesignPatterns { get; set; } = [];
    public List<CodeReadinessOwaspViewModel> OwaspAssessments { get; set; } = [];
    public List<CodeReadinessModuleViewModel> RelatedModules { get; set; } = [];
}

public class CodeReadinessCategoryViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-circle";
}

public class CodeReadinessFindingViewModel
{
    public string Severity { get; set; } = "Low";
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? CodeUrl { get; set; }
    public int Confidence { get; set; } = 100;
}

public class CodeReadinessLayerViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-box";
    public int FileCount { get; set; }
}

public class CodeReadinessSolidCheckViewModel
{
    public string Principle { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Source review required";
}

public class CodeReadinessDesignPatternViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public string Status { get; set; } = "Partial";
    public string Summary { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public List<CodeReadinessPatternEvidenceViewModel> EvidenceFiles { get; set; } = [];
}

public class CodeReadinessPatternEvidenceViewModel
{
    public string Path { get; set; } = string.Empty;
    public string? CodeUrl { get; set; }
}

public class CodeReadinessOwaspViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public string Summary { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? CodeUrl { get; set; }
    public int Confidence { get; set; }
}

public class CodeReadinessModuleViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Status { get; set; } = "Needs Review";
    public int RepositorySignalCount { get; set; }
    public int FeatureCount { get; set; }
    public int IncompleteFeatureCount { get; set; }
    public int OpenBugCount { get; set; }
    public int TestCount { get; set; }
    public int FailedTestCount { get; set; }
    public int PendingTestCount { get; set; }
    public List<string> EvidencePaths { get; set; } = [];
    public string? CodeUrl { get; set; }
}
