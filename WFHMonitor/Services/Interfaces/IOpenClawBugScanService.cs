using WFHMonitor.Models;

namespace WFHMonitor.Services.Interfaces;

public interface IOpenClawBugScanService
{
    int MaxFindingsPerScan { get; }

    Task<OpenClawBugScanResult> ScanProjectAsync(
        ChangeRequest project,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default);

    Task<OpenClawBugFixResult> FixBugAsync(
        BugReport bug,
        string? fixAgentId = null,
        CancellationToken cancellationToken = default);

    Task<OpenClawFeatureImplementResult> ImplementFeatureAsync(
        ProjectFeature feature,
        ChangeRequest? project = null,
        string? featureAgentId = null,
        CancellationToken cancellationToken = default);
}

public sealed record OpenClawBugScanResult(
    bool Succeeded,
    string Error,
    IReadOnlyList<OpenClawBugFinding> Findings);

public sealed record OpenClawBugFinding(
    string Title,
    string Description,
    string Workflow,
    string StepsToReproduce,
    string ModuleImpacted);

public sealed record OpenClawBugFixResult(
    bool Succeeded,
    string Error,
    string FixPlan);

public sealed record OpenClawFeatureImplementResult(
    bool Succeeded,
    string Error,
    string ImplementationPlan);
