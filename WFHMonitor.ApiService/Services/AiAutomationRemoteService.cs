using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using WFHMonitor.Configuration;
using WFHMonitor.Infrastructure.Json;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public sealed class AiAutomationRemoteService(
    HttpClient httpClient,
    IOptions<AiAutomationServiceSettings> settings,
    ILogger<AiAutomationRemoteService> logger)
    : IAiAutomationService, IAiAccountService
{
    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();
    private readonly AiAutomationServiceSettings _settings = settings.Value;

    public int MaxFindingsPerScan =>
        Math.Clamp(_settings.MaxFindingsPerScan, 1, 20);

    public Task<AiBugScanResult> ScanProjectAsync(
        ChangeRequest project,
        string? workspacePath = null,
        CancellationToken cancellationToken = default,
        bool useSecurityPrompt = false) =>
        SendAutomationAsync<AiScanProjectRequest, AiBugScanResult>(
            "api/automation/scan-project",
            new(project, useSecurityPrompt, workspacePath),
            "scan a project",
            cancellationToken);

    public Task<AiBugScanResult> ScanModuleAsync(
        ChangeRequest project,
        string moduleName,
        string? workspacePath = null,
        CancellationToken cancellationToken = default,
        bool useSecurityPrompt = false) =>
        SendAutomationAsync<AiScanModuleRequest, AiBugScanResult>(
            "api/automation/scan-module",
            new(project, moduleName, useSecurityPrompt, workspacePath),
            "scan a module",
            cancellationToken);

    public Task<AiBugFixResult> FixBugAsync(
        BugReport bug,
        string? workspacePath = null,
        CancellationToken cancellationToken = default) =>
        SendAutomationAsync<AiFixBugRequest, AiBugFixResult>(
            "api/automation/fix-bug",
            new(bug, workspacePath),
            "fix a bug",
            cancellationToken);

    public Task<AiFeatureGuidanceResult> ImplementFeatureAsync(
        ProjectFeature feature,
        ChangeRequest? project = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default) =>
        SendAutomationAsync<
            AiFeatureGuidanceRequest,
            AiFeatureGuidanceResult>(
            "api/automation/implement-feature",
            new(feature, project, workspacePath),
            "prepare feature guidance",
            cancellationToken);

    public Task<AiTestCaseGenerationResult> GenerateTestCasesAsync(
        ChangeRequest project,
        string? workspacePath = null,
        CancellationToken cancellationToken = default) =>
        SendAutomationAsync<
            AiGenerateTestsRequest,
            AiTestCaseGenerationResult>(
            "api/automation/generate-tests",
            new(project, workspacePath),
            "generate test cases",
            cancellationToken);

    public Task<AiProjectHealthResult> AnalyzeProjectHealthAsync(
        ChangeRequest project,
        int totalBugs,
        int openBugs,
        int featureCount,
        int repositoryFeatureCount,
        int timelineDays,
        int complexityScore,
        string? workspacePath = null,
        CancellationToken cancellationToken = default) =>
        SendAutomationAsync<
            AiAnalyzeHealthRequest,
            AiProjectHealthResult>(
            "api/automation/analyze-health",
            new(
                project,
                totalBugs,
                openBugs,
                featureCount,
                repositoryFeatureCount,
                timelineDays,
                complexityScore,
                workspacePath),
            "analyze project health",
            cancellationToken);

    public Task<BrainstormDesignBlueprint> GenerateBrainstormAsync(
        BrainstormGenerateDesignRequest request,
        CancellationToken cancellationToken = default) =>
        SendRequiredAsync<
            BrainstormGenerateDesignRequest,
            BrainstormDesignBlueprint>(
            HttpMethod.Post,
            "api/automation/brainstorm",
            request,
            cancellationToken);

    public Task<AiModuleImageGenerationResult> GenerateModuleImagesAsync(
        AiModuleImageGenerationRequest request,
        CancellationToken cancellationToken = default) =>
        SendAutomationAsync<
            AiModuleImageGenerationRequest,
            AiModuleImageGenerationResult>(
            "api/automation/module-images",
            request,
            "generate module images",
            cancellationToken);

    public Task<AiAccountStatus> GetStatusAsync(
        CancellationToken cancellationToken = default) =>
        SendRequiredAsync<object, AiAccountStatus>(
            HttpMethod.Get,
            "api/automation/account",
            null,
            cancellationToken);

    public Task<AiLoginStartResult> StartLoginAsync(
        CancellationToken cancellationToken = default) =>
        SendRequiredAsync<object, AiLoginStartResult>(
            HttpMethod.Post,
            "api/automation/account/login",
            new { },
            cancellationToken);

    public Task<AiLoginStatus> GetLoginStatusAsync(
        string loginId,
        CancellationToken cancellationToken = default) =>
        SendRequiredAsync<object, AiLoginStatus>(
            HttpMethod.Get,
            $"api/automation/account/login/{Uri.EscapeDataString(loginId)}",
            null,
            cancellationToken);

    public Task CancelLoginAsync(
        string loginId,
        CancellationToken cancellationToken = default) =>
        SendNoContentAsync(
            $"api/automation/account/login/{Uri.EscapeDataString(loginId)}/cancel",
            cancellationToken);

    public Task LogoutAsync(CancellationToken cancellationToken = default) =>
        SendNoContentAsync(
            "api/automation/account/logout",
            cancellationToken);

    private async Task<TResponse> SendAutomationAsync<TRequest, TResponse>(
        string requestUri,
        TRequest request,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SendRequiredAsync<TRequest, TResponse>(
                HttpMethod.Post,
                requestUri,
                request,
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "The AI automation service failed to {Operation}.",
                operation);
            return CreateUnavailableResponse<TResponse>();
        }
    }

    private async Task<TResponse> SendRequiredAsync<TRequest, TResponse>(
        HttpMethod method,
        string requestUri,
        TRequest? request,
        CancellationToken cancellationToken)
    {
        using var message = CreateMessage(method, requestUri);
        if (request is not null)
        {
            message.Content = JsonContent.Create(
                request,
                options: SerializerOptions);
        }

        using var response = await httpClient.SendAsync(
            message,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(
                   SerializerOptions,
                   cancellationToken)
               ?? throw new InvalidOperationException(
                   "The AI automation service returned an empty response.");
    }

    private async Task SendNoContentAsync(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var message = CreateMessage(HttpMethod.Post, requestUri);
        using var response = await httpClient.SendAsync(
            message,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateMessage(
        HttpMethod method,
        string requestUri)
    {
        var message = new HttpRequestMessage(method, requestUri);
        message.Headers.TryAddWithoutValidation(
            "X-Service-Secret",
            _settings.SharedSecret);
        return message;
    }

    private static TResponse CreateUnavailableResponse<TResponse>()
    {
        object result = typeof(TResponse) switch
        {
            var type when type == typeof(AiBugScanResult) =>
                new AiBugScanResult(
                    false,
                    "The AI automation service is unavailable.",
                    [],
                    string.Empty),
            var type when type == typeof(AiBugFixResult) =>
                new AiBugFixResult(
                    false,
                    "The AI automation service is unavailable.",
                    string.Empty),
            var type when type == typeof(AiFeatureGuidanceResult) =>
                new AiFeatureGuidanceResult(
                    false,
                    "The AI automation service is unavailable.",
                    string.Empty),
            var type when type == typeof(AiTestCaseGenerationResult) =>
                new AiTestCaseGenerationResult(
                    false,
                    "The AI automation service is unavailable.",
                    [],
                    string.Empty),
            var type when type == typeof(AiProjectHealthResult) =>
                new AiProjectHealthResult(
                    false,
                    "The AI automation service is unavailable.",
                    0,
                    "Unknown",
                    string.Empty,
                    "Unknown",
                    [],
                    string.Empty),
            var type when type == typeof(AiModuleImageGenerationResult) =>
                new AiModuleImageGenerationResult(
                    false,
                    "The AI automation service is unavailable.",
                    []),
            _ => throw new InvalidOperationException(
                $"No unavailable response is configured for {typeof(TResponse).Name}.")
        };
        return (TResponse)result;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new SafeApplicationUserJsonConverter());
        return options;
    }
}
