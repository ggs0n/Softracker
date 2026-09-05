using System.Text.Json;
using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public sealed record BrainstormGenerateDesignRequest(
    string Summary,
    string Technology,
    string UserCount,
    string Features,
    string? CloudHostingTarget = null)
{
    public BrainstormGenerateDesignRequest Trimmed() =>
        new(
            Summary?.Trim() ?? string.Empty,
            Technology?.Trim() ?? string.Empty,
            UserCount?.Trim() ?? string.Empty,
            Features?.Trim() ?? string.Empty,
            CloudHostingTarget?.Trim());
}

public sealed record BrainstormImportChatGptResultRequest(
    string Summary,
    string Technology,
    string UserCount,
    string Features,
    string ChatGptResult,
    string? CloudHostingTarget = null)
{
    public BrainstormGenerateDesignRequest ToGenerateRequest() =>
        new(
            Summary?.Trim() ?? string.Empty,
            Technology?.Trim() ?? string.Empty,
            UserCount?.Trim() ?? string.Empty,
            Features?.Trim() ?? string.Empty,
            CloudHostingTarget?.Trim());
}

public sealed record BrainstormDesignBlueprint(
    string Title,
    string RecommendedArchitecture,
    IReadOnlyList<string> MainComponents,
    IReadOnlyList<BrainstormDeploymentService>? DeploymentServices,
    string DatabaseStorageRecommendation,
    string ApiBackendRecommendation,
    string ScalingAdvice,
    string SecurityNotes,
    BrainstormCostEstimate? CostEstimate,
    IReadOnlyList<string> RisksTradeoffs,
    IReadOnlyList<string> NextSteps,
    IReadOnlyList<BrainstormTableSchema>? TableSchemas,
    IReadOnlyList<BrainstormDesignDiagram> Diagrams,
    string SourceMode,
    string? Notice,
    IReadOnlyList<BrainstormServiceDetail>? ServiceDetails = null,
    IReadOnlyList<BrainstormRestEndpoint>? RestEndpoints = null,
    IReadOnlyList<BrainstormGrpcContract>? GrpcContracts = null,
    IReadOnlyList<BrainstormBatchJob>? BatchJobs = null);

public sealed record BrainstormDeploymentService(
    string Module,
    string RecommendedService,
    string Runtime,
    string Reason);

public sealed record BrainstormServiceDetail(
    string Name,
    string Type,
    string Responsibility,
    string Runtime,
    string Deployment,
    string DataOwnership,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<string> Communication);

public sealed record BrainstormRestEndpoint(
    string Service,
    string Method,
    string Path,
    string Purpose,
    string Authentication,
    string Request,
    string Response,
    IReadOnlyList<string> StatusCodes);

public sealed record BrainstormGrpcContract(
    string Service,
    string Contract,
    string RpcMethod,
    string RequestMessage,
    string ResponseMessage,
    string Streaming,
    string Purpose);

public sealed record BrainstormBatchJob(
    string Name,
    string OwnerService,
    string Trigger,
    string Schedule,
    string Responsibility,
    string Input,
    string Output,
    string RetryPolicy,
    string IdempotencyStrategy);

public sealed record BrainstormCostEstimate(
    string Currency,
    string MonthlyRange,
    string Summary,
    IReadOnlyList<BrainstormCostLineItem> LineItems,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> CostOptimizations);

public sealed record BrainstormCostLineItem(
    string Name,
    string MonthlyRange,
    string Notes);

public sealed record BrainstormTableSchema(
    string Name,
    string Purpose,
    IReadOnlyList<BrainstormTableColumn> Columns,
    IReadOnlyList<string> Relationships);

public sealed record BrainstormTableColumn(
    string Name,
    string Type,
    bool IsPrimaryKey,
    bool IsForeignKey,
    string Notes);

public sealed record BrainstormDesignDiagram(
    string Title,
    string Kind,
    string Mermaid);

public sealed record BrainstormDesignProjectSummary(
    int Id,
    string Title,
    string Summary,
    string Technology,
    string? CloudHostingTarget,
    string UserCount,
    DateTimeOffset CreatedAt,
    string SourceMode);

public sealed record BrainstormDesignProjectDetails(
    int Id,
    string Summary,
    string Technology,
    string? CloudHostingTarget,
    string UserCount,
    string Features,
    DateTimeOffset CreatedAt,
    BrainstormDesignBlueprint Blueprint)
{
    public static BrainstormDesignProjectDetails From(
        BrainstormDesignProject project) =>
        new(
            project.Id,
            project.Summary,
            project.Technology,
            project.CloudHostingTarget,
            project.UserCount,
            project.Features,
            project.CreatedAt,
            JsonSerializer.Deserialize<BrainstormDesignBlueprint>(
                project.BlueprintJson,
                BrainstormJsonOptions.Default)
            ?? throw new InvalidOperationException(
                "Stored Brainstorm blueprint is invalid."));
}

public static class BrainstormJsonOptions
{
    public static readonly JsonSerializerOptions Default =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
}

public static class BrainstormRequestValidator
{
    public static Dictionary<string, string[]> Validate(
        BrainstormGenerateDesignRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        AddIfBlank(errors, nameof(request.Summary), request.Summary,
            "A simple system summary is required.");
        AddIfBlank(errors, nameof(request.Technology), request.Technology,
            "The technology stack is required.");
        AddIfBlank(errors, nameof(request.UserCount), request.UserCount,
            "The expected user count is required.");
        AddIfBlank(errors, nameof(request.Features), request.Features,
            "At least one main feature is required.");
        AddIfTooLong(errors, nameof(request.Summary), request.Summary, 4_000);
        AddIfTooLong(errors, nameof(request.Technology), request.Technology, 2_000);
        AddIfTooLong(errors, nameof(request.CloudHostingTarget),
            request.CloudHostingTarget, 120);
        AddIfTooLong(errors, nameof(request.UserCount), request.UserCount, 100);
        AddIfTooLong(errors, nameof(request.Features), request.Features, 6_000);
        return errors;
    }

    public static Dictionary<string, string[]> Validate(
        BrainstormImportChatGptResultRequest request)
    {
        var errors = Validate(request.ToGenerateRequest());
        AddIfBlank(errors, nameof(request.ChatGptResult), request.ChatGptResult,
            "ChatGPT result JSON is required.");
        AddIfTooLong(errors, nameof(request.ChatGptResult),
            request.ChatGptResult, 1_000_000);
        return errors;
    }

    private static void AddIfBlank(
        IDictionary<string, string[]> errors,
        string key,
        string? value,
        string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            errors[key] = [message];
    }

    private static void AddIfTooLong(
        IDictionary<string, string[]> errors,
        string key,
        string? value,
        int maximumLength)
    {
        if (value?.Length > maximumLength)
            errors[key] = [$"Maximum length is {maximumLength:N0} characters."];
    }
}
