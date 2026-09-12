using System.ComponentModel.DataAnnotations;
using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class ProjectKickStartInputViewModel
{
    [Required, StringLength(2000)]
    [Display(Name = "Simple summary of system")]
    public string Summary { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    [Display(Name = "Technology use")]
    public string Technology { get; set; } = string.Empty;

    [StringLength(120)]
    [Display(Name = "Cloud / hosting target")]
    public string? CloudHostingTarget { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "How many users use")]
    public string UserCount { get; set; } = string.Empty;

    [Required, StringLength(2000)]
    [Display(Name = "Main features")]
    public string Features { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Theme / Describe UI")]
    public string? UiDirection { get; set; }
}

public class ProjectKickStartPageViewModel
{
    public ProjectKickStartInputViewModel Input { get; set; } = new();
    public ProjectKickStartDesign? SelectedDesign { get; set; }
    public ProjectKickStartBlueprint? Blueprint { get; set; }
    public List<ProjectKickStartDesign> History { get; set; } = [];
    public bool IsCodexAvailable { get; set; }
    public bool IsCodexConnected { get; set; }
    public bool IsCodexChatGptLogin { get; set; }
    public string CodexStatusMessage { get; set; } = string.Empty;
}

public record ProjectKickStartBlueprint(
    string Title,
    string RecommendedArchitecture,
    IReadOnlyList<string> MainComponents,
    IReadOnlyList<ProjectKickStartDeploymentService> DeploymentServices,
    string DatabaseStorageRecommendation,
    string ApiBackendRecommendation,
    string ScalingAdvice,
    string SecurityNotes,
    ProjectKickStartCostEstimate CostEstimate,
    IReadOnlyList<string> RisksTradeoffs,
    IReadOnlyList<string> NextSteps,
    IReadOnlyList<ProjectKickStartTableSchema> TableSchemas,
    string SourceMode,
    string? Notice,
    ProjectKickStartMvpPlan? Mvp = null,
    IReadOnlyList<ProjectKickStartUserFlow>? UserFlows = null,
    ProjectKickStartVisualPlan? VisualPlan = null);

public record ProjectKickStartDeploymentService(string Module, string RecommendedService, string Runtime, string Reason);
public record ProjectKickStartCostEstimate(string Currency, string MonthlyRange, string Summary, IReadOnlyList<ProjectKickStartCostLineItem> LineItems, IReadOnlyList<string> Assumptions, IReadOnlyList<string> CostOptimizations);
public record ProjectKickStartCostLineItem(string Name, string MonthlyRange, string Notes);
public record ProjectKickStartTableSchema(string Name, string Purpose, IReadOnlyList<ProjectKickStartTableColumn> Columns, IReadOnlyList<string> Relationships);
public record ProjectKickStartTableColumn(string Name, string Type, bool IsPrimaryKey, bool IsForeignKey, string Notes);
public record ProjectKickStartMvpPlan(
    string SystemOverview,
    string ProblemSolved,
    string CoreValue,
    IReadOnlyList<ProjectKickStartMvpCapability> CoreCapabilities,
    IReadOnlyList<string> OutOfScope,
    IReadOnlyList<string> SuccessCriteria);
public record ProjectKickStartMvpCapability(string Name, string Description, string AcceptanceOutcome);
public record ProjectKickStartUserFlow(string UserType, string Purpose, IReadOnlyList<string> Steps);
public record ProjectKickStartVisualPlan(
    string Theme,
    string Palette,
    string Typography,
    string HeroScale,
    string NarrativeSpine,
    IReadOnlyList<ProjectKickStartPageImageSample> PageSamples);
public record ProjectKickStartPageImageSample(
    string PageName,
    string Route,
    string Purpose,
    string SectionName,
    string Headline,
    string SupportingCopy,
    string PrimaryAction,
    string CompositionAnchor,
    string BackgroundMode,
    string VisualDirection,
    IReadOnlyList<string> KeyElements,
    string ImagePrompt,
    string? ImageFileName = null);
