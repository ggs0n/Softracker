using WFHMonitor.Models;

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
