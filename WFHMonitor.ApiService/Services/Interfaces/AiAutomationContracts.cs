using WFHMonitor.Models;

namespace WFHMonitor.Services.Interfaces;

public sealed record AiTestCaseGenerationResult(
    bool Succeeded,
    string Error,
    IReadOnlyList<AiGeneratedTestCase> TestCases,
    string AutomationResponseText);

public sealed record AiGeneratedTestCase(
    string Name,
    string Description,
    string Module,
    string Category,
    string Environment);

public sealed record AiBugScanResult(
    bool Succeeded,
    string Error,
    IReadOnlyList<AiBugFinding> Findings,
    string AutomationResponseText);

public sealed record AiBugFinding(
    string Title,
    string Description,
    string Workflow,
    string StepsToReproduce,
    string ModuleImpacted,
    string Severity,
    IReadOnlyList<string> ScreenshotPaths);

public sealed record AiBugFixResult(
    bool Succeeded,
    string Error,
    string FixPlan);

public sealed record AiFeatureGuidanceResult(
    bool Succeeded,
    string Error,
    string ImplementationPlan);

public sealed record AiProjectHealthResult(
    bool Succeeded,
    string Error,
    int Score,
    string Label,
    string Summary,
    string Complexity,
    IReadOnlyList<string> Factors,
    string AutomationResponseText);

public sealed record AiScanProjectRequest(
    ChangeRequest Project,
    bool UseSecurityPrompt,
    string? WorkspacePath = null);

public sealed record AiScanModuleRequest(
    ChangeRequest Project,
    string ModuleName,
    bool UseSecurityPrompt,
    string? WorkspacePath = null);

public sealed record AiFixBugRequest(
    BugReport Bug,
    string? WorkspacePath = null);

public sealed record AiFeatureGuidanceRequest(
    ProjectFeature Feature,
    ChangeRequest? Project,
    string? WorkspacePath = null);

public sealed record AiGenerateTestsRequest(
    ChangeRequest Project,
    string? WorkspacePath = null);

public sealed record AiAnalyzeHealthRequest(
    ChangeRequest Project,
    int TotalBugs,
    int OpenBugs,
    int FeatureCount,
    int RepositoryFeatureCount,
    int TimelineDays,
    int ComplexityScore,
    string? WorkspacePath = null);

public sealed record AiModuleImageGenerationRequest(
    string SystemTitle,
    string SystemSummary,
    string Technology,
    IReadOnlyList<AiModuleImageContext> Modules);

public sealed record AiModuleImageContext(
    string ModuleName,
    string Responsibility,
    string Runtime,
    string Deployment,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<string> OwnedData,
    IReadOnlyList<string> BackgroundJobs);

public sealed record AiModuleImageGenerationResult(
    bool Succeeded,
    string Error,
    IReadOnlyList<AiGeneratedModuleImage> Images);

public sealed record AiGeneratedModuleImage(
    string ModuleName,
    string MimeType,
    string Base64Data,
    string? RevisedPrompt);
