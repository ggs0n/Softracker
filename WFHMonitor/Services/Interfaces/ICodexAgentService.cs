using WFHMonitor.Models;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface ICodexBugScanService
{
    int MaxFindingsPerScan { get; }

    Task<CodexBugScanResult> ScanProjectAsync(
        ChangeRequest project,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default,
        bool useSecurityPrompt = false);

    Task<CodexBugScanResult> ScanModuleAsync(
        ChangeRequest project,
        string moduleName,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default,
        bool useSecurityPrompt = false);

    Task<CodexBugFixResult> FixBugAsync(
        BugReport bug,
        string? fixAgentId = null,
        CancellationToken cancellationToken = default);

    Task<CodexFeatureImplementResult> ImplementFeatureAsync(
        ProjectFeature feature,
        ChangeRequest? project = null,
        string? featureAgentId = null,
        CancellationToken cancellationToken = default);

    Task<CodexTestCaseGenResult> GenerateTestCasesAsync(
        ChangeRequest project,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default);

    Task<CodexProjectHealthResult> AnalyzeProjectHealthAsync(
        ChangeRequest project,
        int totalBugs,
        int openBugs,
        int featureCount,
        int repositoryFeatureCount,
        int timelineDays,
        int complexityScore,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default);

    Task<CodexCodeReadinessResult> ScanCodeReadinessAsync(
        ChangeRequest project,
        string repositoryPath,
        string commitSha,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default);

    Task<CodexProjectKickStartResult> GenerateProjectKickStartAsync(
        ProjectKickStartInputViewModel input,
        string? agentId = null,
        CancellationToken cancellationToken = default);

    Task<CodexProjectImageGenerationResult> GenerateProjectKickStartImagesAsync(
        ProjectKickStartBlueprint blueprint,
        int designId,
        string? agentId = null,
        CancellationToken cancellationToken = default);

    string? ResolveProjectKickStartImagePath(int designId, string? fileName);

    Task DeleteProjectKickStartImagesAsync(int designId);
}

public sealed record CodexProjectKickStartResult(
    bool Succeeded,
    string Error,
    ProjectKickStartBlueprint? Blueprint);

public sealed record CodexProjectImageGenerationResult(
    bool Succeeded,
    string Error,
    IReadOnlyList<CodexGeneratedProjectImage> Images);

public sealed record CodexGeneratedProjectImage(
    int PageIndex,
    string FileName);

public sealed class CodexCodeReadinessResult
{
    public bool Succeeded { get; set; }
    public string Error { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int AnalyzedFiles { get; set; }
    public List<CodexCodeReadinessCategory> Categories { get; set; } = [];
    public List<CodexCodeReadinessFinding> Findings { get; set; } = [];
    public List<CodexSolidReview> Solid { get; set; } = [];
    public List<CodexDesignPattern> DesignPatterns { get; set; } = [];
    public List<CodexOwaspAssessment> Owasp { get; set; } = [];
    public List<CodexCodePracticeAssessment> ValidationAndNullHandling { get; set; } = [];
    public List<CodexCodePracticeAssessment> LoggingAndExceptionHandling { get; set; } = [];
}

public sealed class CodexCodeReadinessCategory
{
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public sealed class CodexCodeReadinessFinding
{
    public string Principle { get; set; } = "None";
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "Low";
    public string Title { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int? Line { get; set; }
    public int Confidence { get; set; }
}

public sealed class CodexSolidReview
{
    public string Principle { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public string Summary { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int? Line { get; set; }
    public int Confidence { get; set; }
}

public sealed class CodexDesignPattern
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public string Status { get; set; } = "Partial";
    public string Summary { get; set; } = string.Empty;
    public List<string> Files { get; set; } = [];
    public int Confidence { get; set; }
}

public sealed class CodexOwaspAssessment
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public string Summary { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int? Line { get; set; }
    public int Confidence { get; set; }
}

public sealed class CodexCodePracticeAssessment
{
    public string Status { get; set; } = "Unknown";
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int? Line { get; set; }
    public int Confidence { get; set; }
}

public sealed record CodexTestCaseGenResult(
    bool Succeeded,
    string Error,
    IReadOnlyList<CodexGeneratedTestCase> TestCases,
    string AgentResponseText);

public sealed record CodexGeneratedTestCase(
    string Name,
    string Description,
    string Module,
    string Category,
    string Environment);

public sealed record CodexBugScanResult(
    bool Succeeded,
    string Error,
    IReadOnlyList<CodexBugFinding> Findings,
    string AgentResponseText);

public sealed record CodexBugFinding(
    string Title,
    string Description,
    string Workflow,
    string StepsToReproduce,
    string ModuleImpacted,
    string Severity,
    IReadOnlyList<string> ScreenshotPaths);

public sealed record CodexBugFixResult(
    bool Succeeded,
    string Error,
    string FixPlan,
    string? PullRequestUrl);

public sealed record CodexFeatureImplementResult(
    bool Succeeded,
    string Error,
    string ImplementationPlan);

public sealed record CodexProjectHealthResult(
    bool Succeeded,
    string Error,
    int Score,
    string Label,
    string Summary,
    string Complexity,
    IReadOnlyList<string> Factors,
    string AgentResponseText);
