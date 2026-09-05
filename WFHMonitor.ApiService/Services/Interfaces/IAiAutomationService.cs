using WFHMonitor.Models;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface IAiAutomationService
{
    int MaxFindingsPerScan { get; }

    Task<AiBugScanResult> ScanProjectAsync(
        ChangeRequest project,
        string? workspacePath = null,
        CancellationToken cancellationToken = default,
        bool useSecurityPrompt = false);

    Task<AiBugScanResult> ScanModuleAsync(
        ChangeRequest project,
        string moduleName,
        string? workspacePath = null,
        CancellationToken cancellationToken = default,
        bool useSecurityPrompt = false);

    Task<AiBugFixResult> FixBugAsync(
        BugReport bug,
        string? workspacePath = null,
        CancellationToken cancellationToken = default);

    Task<AiFeatureGuidanceResult> ImplementFeatureAsync(
        ProjectFeature feature,
        ChangeRequest? project = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default);

    Task<AiTestCaseGenerationResult> GenerateTestCasesAsync(
        ChangeRequest project,
        string? workspacePath = null,
        CancellationToken cancellationToken = default);

    Task<AiProjectHealthResult> AnalyzeProjectHealthAsync(
        ChangeRequest project,
        int totalBugs,
        int openBugs,
        int featureCount,
        int repositoryFeatureCount,
        int timelineDays,
        int complexityScore,
        string? workspacePath = null,
        CancellationToken cancellationToken = default);

    Task<BrainstormDesignBlueprint> GenerateBrainstormAsync(
        BrainstormGenerateDesignRequest request,
        CancellationToken cancellationToken = default);

    Task<AiModuleImageGenerationResult> GenerateModuleImagesAsync(
        AiModuleImageGenerationRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAiAccountService
{
    Task<AiAccountStatus> GetStatusAsync(
        CancellationToken cancellationToken = default);

    Task<AiLoginStartResult> StartLoginAsync(
        CancellationToken cancellationToken = default);

    Task<AiLoginStatus> GetLoginStatusAsync(
        string loginId,
        CancellationToken cancellationToken = default);

    Task CancelLoginAsync(
        string loginId,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);
}

public sealed record AiAccountStatus(
    string State,
    string? Email,
    string? PlanType,
    string Model,
    int? UsedPercent,
    DateTimeOffset? ResetsAt,
    string? LimitReachedType,
    string? Message);

public sealed record AiLoginStartResult(
    string LoginId,
    string AuthUrl);

public sealed record AiLoginStatus(
    string LoginId,
    string State,
    string? Message);
