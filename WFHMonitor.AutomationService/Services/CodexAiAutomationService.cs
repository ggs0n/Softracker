using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WFHMonitor.AutomationService.Configuration;
using WFHMonitor.Models;
using WFHMonitor.Services;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.AutomationService.Services;

public sealed class CodexAiAutomationService :
    IAiAutomationService,
    IAiAccountService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly ICodexAppServerClient _codex;
    private readonly PromptAssetLoader _assets;
    private readonly AiAutomationSettings _settings;
    private readonly string _workspaceRoot;
    private readonly ILogger<CodexAiAutomationService> _logger;

    public CodexAiAutomationService(
        ICodexAppServerClient codex,
        PromptAssetLoader assets,
        IOptions<AiAutomationSettings> settings,
        IWebHostEnvironment environment,
        ILogger<CodexAiAutomationService> logger)
    {
        _codex = codex;
        _assets = assets;
        _settings = settings.Value;
        _workspaceRoot = Path.GetFullPath(
            Path.IsPathRooted(_settings.WorkspaceRoot)
                ? _settings.WorkspaceRoot
                : Path.Combine(
                    environment.ContentRootPath,
                    _settings.WorkspaceRoot));
        _logger = logger;
    }

    public int MaxFindingsPerScan =>
        Math.Clamp(_settings.MaxFindingsPerScan, 1, 20);

    public int MaxModuleImages =>
        Math.Clamp(_settings.MaxModuleImages, 1, 10);

    public Task<AiAccountStatus> GetStatusAsync(
        CancellationToken cancellationToken = default) =>
        _codex.GetAccountStatusAsync(cancellationToken);

    public Task<AiLoginStartResult> StartLoginAsync(
        CancellationToken cancellationToken = default) =>
        _codex.StartLoginAsync(cancellationToken);

    public Task<AiLoginStatus> GetLoginStatusAsync(
        string loginId,
        CancellationToken cancellationToken = default) =>
        _codex.GetLoginStatusAsync(loginId, cancellationToken);

    public Task CancelLoginAsync(
        string loginId,
        CancellationToken cancellationToken = default) =>
        _codex.CancelLoginAsync(loginId, cancellationToken);

    public Task LogoutAsync(
        CancellationToken cancellationToken = default) =>
        _codex.LogoutAsync(cancellationToken);

    public async Task<AiBugScanResult> ScanProjectAsync(
        ChangeRequest project,
        string? workspacePath = null,
        CancellationToken cancellationToken = default,
        bool useSecurityPrompt = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        try
        {
            var prompt = await ComposePromptAsync(
                useSecurityPrompt ? "security-scan" : "project-scan",
                ProjectContext(project),
                cancellationToken);
            var result = await RunStructuredAsync<ScanPayload>(
                prompt,
                ResolveWorkspace(workspacePath, "scan"),
                false,
                "bug-scan",
                cancellationToken);
            var findings = (result.Payload.Findings ?? [])
                .Take(MaxFindingsPerScan)
                .Select(ToFinding)
                .ToArray();
            return new AiBugScanResult(
                true,
                string.Empty,
                findings,
                result.Turn.OutputText);
        }
        catch (Exception exception)
        {
            return FailedScan(exception, "Project scan");
        }
    }

    public async Task<AiBugScanResult> ScanModuleAsync(
        ChangeRequest project,
        string moduleName,
        string? workspacePath = null,
        CancellationToken cancellationToken = default,
        bool useSecurityPrompt = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(moduleName))
            throw new ArgumentException(
                "A module name is required.",
                nameof(moduleName));
        try
        {
            var context =
                $"{ProjectContext(project)}\nTarget module: {moduleName.Trim()}";
            var prompt = await ComposePromptAsync(
                useSecurityPrompt ? "security-scan" : "module-scan",
                context,
                cancellationToken);
            var result = await RunStructuredAsync<ScanPayload>(
                prompt,
                ResolveWorkspace(workspacePath, "scan"),
                false,
                "bug-scan",
                cancellationToken);
            var findings = (result.Payload.Findings ?? [])
                .Take(MaxFindingsPerScan)
                .Select(ToFinding)
                .ToArray();
            return new AiBugScanResult(
                true,
                string.Empty,
                findings,
                result.Turn.OutputText);
        }
        catch (Exception exception)
        {
            return FailedScan(exception, "Module scan");
        }
    }

    public async Task<AiBugFixResult> FixBugAsync(
        BugReport bug,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bug);
        try
        {
            var prompt = await ComposePromptAsync(
                "fix-bug",
                BugContext(bug),
                cancellationToken);
            var result = await RunStructuredAsync<FixPayload>(
                prompt,
                ResolveWorkspace(workspacePath, "fix"),
                true,
                "bug-fix",
                cancellationToken);
            var plan = FormatFixPlan(result.Payload, result.Turn);
            return new AiBugFixResult(
                true,
                string.Empty,
                plan);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Codex bug-fix operation failed.");
            return new AiBugFixResult(
                false,
                SafeOperationError(exception, "Bug fix"),
                string.Empty);
        }
    }

    public async Task<AiFeatureGuidanceResult> ImplementFeatureAsync(
        ProjectFeature feature,
        ChangeRequest? project = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feature);
        try
        {
            var context = $"""
                           Feature: {feature.FeatureNumber} - {feature.Name}
                           Description: {feature.Description}
                           Module: {feature.ModuleImpacted}
                           Status: {feature.Status}
                           Priority: {feature.Priority}
                           Project: {project?.CrNumber} - {project?.Title}
                           Technology: {project?.TechnologyStack}
                           """;
            var prompt = await ComposePromptAsync(
                "feature-guidance",
                context,
                cancellationToken);
            var turn = await _codex.RunTurnAsync(
                new CodexTurnRequest(
                    prompt,
                    ResolveWorkspace(workspacePath, "guidance"),
                    false),
                cancellationToken);
            return new AiFeatureGuidanceResult(
                true,
                string.Empty,
                turn.OutputText);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Codex feature-guidance operation failed.");
            return new AiFeatureGuidanceResult(
                false,
                SafeOperationError(exception, "Feature guidance"),
                string.Empty);
        }
    }

    public async Task<AiTestCaseGenerationResult> GenerateTestCasesAsync(
        ChangeRequest project,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        try
        {
            var prompt = await ComposePromptAsync(
                "test-generation",
                ProjectContext(project),
                cancellationToken);
            var result = await RunStructuredAsync<TestCasesPayload>(
                prompt,
                ResolveWorkspace(workspacePath, "tests"),
                false,
                "test-cases",
                cancellationToken);
            var testCases = (result.Payload.TestCases ?? [])
                .Take(20)
                .Select(test => new AiGeneratedTestCase(
                    test.Name ?? string.Empty,
                    test.Description ?? string.Empty,
                    test.Module ?? string.Empty,
                    test.Category ?? "Regression",
                    test.Environment ?? "Dev"))
                .ToArray();
            return new AiTestCaseGenerationResult(
                true,
                string.Empty,
                testCases,
                result.Turn.OutputText);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Codex test-generation operation failed.");
            return new AiTestCaseGenerationResult(
                false,
                SafeOperationError(exception, "Test generation"),
                [],
                string.Empty);
        }
    }

    public async Task<AiProjectHealthResult> AnalyzeProjectHealthAsync(
        ChangeRequest project,
        int totalBugs,
        int openBugs,
        int featureCount,
        int repositoryFeatureCount,
        int timelineDays,
        int complexityScore,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        try
        {
            var context = $"""
                           {ProjectContext(project)}
                           Total bugs: {Math.Max(totalBugs, 0)}
                           Open bugs: {Math.Max(openBugs, 0)}
                           Features: {Math.Max(featureCount, 0)}
                           Repository features: {Math.Max(repositoryFeatureCount, 0)}
                           Timeline days: {Math.Max(timelineDays, 0)}
                           Complexity input: {Math.Clamp(complexityScore, 0, 100)}
                           """;
            var prompt = await ComposePromptAsync(
                "health-analysis",
                context,
                cancellationToken);
            var result = await RunStructuredAsync<HealthPayload>(
                prompt,
                ResolveWorkspace(workspacePath, "health"),
                false,
                "project-health",
                cancellationToken);
            return new AiProjectHealthResult(
                true,
                string.Empty,
                Math.Clamp(result.Payload.Score, 0, 100),
                result.Payload.Label ?? "At Risk",
                result.Payload.Summary ?? string.Empty,
                result.Payload.Complexity ?? "Medium",
                result.Payload.Factors ?? [],
                result.Turn.OutputText);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Codex project-health operation failed.");
            return new AiProjectHealthResult(
                false,
                SafeOperationError(exception, "Health analysis"),
                0,
                "Unknown",
                string.Empty,
                "Unknown",
                [],
                string.Empty);
        }
    }

    public async Task<BrainstormDesignBlueprint> GenerateBrainstormAsync(
        BrainstormGenerateDesignRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = $"""
                       System summary: {request.Summary}
                       Technology: {request.Technology}
                       Cloud or hosting target: {request.CloudHostingTarget ?? "No preference"}
                       Expected users: {request.UserCount}
                       Main features: {request.Features}
                       """;
        var prompt = await ComposePromptAsync(
            "brainstorm",
            context,
            cancellationToken);
        var turn = await _codex.RunTurnAsync(
            new CodexTurnRequest(
                prompt,
                ResolveWorkspace(null, "brainstorm"),
                false,
                BrainstormBlueprintSchema.Value.DeepClone()),
            cancellationToken);
        var blueprint =
            JsonSerializer.Deserialize<BrainstormDesignBlueprint>(
                turn.OutputText,
                BrainstormJsonOptions.Default)
            ?? throw new InvalidOperationException(
                "Codex returned an invalid Brainstorm blueprint.");
        if (blueprint.Diagrams.Count != 5)
            throw new InvalidOperationException(
                "Codex did not return the five required diagrams.");
        return blueprint with
        {
            SourceMode = "ChatGPT OAuth",
            Notice = null
        };
    }

    public async Task<AiModuleImageGenerationResult> GenerateModuleImagesAsync(
        AiModuleImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var modules = (request.Modules ?? [])
            .Where(module =>
                !string.IsNullOrWhiteSpace(module.ModuleName))
            .Take(MaxModuleImages)
            .ToArray();
        if (modules.Length == 0)
        {
            return new AiModuleImageGenerationResult(
                false,
                "At least one main module is required.",
                []);
        }

        var workspace = ResolveWorkspace(null, "module-images");
        try
        {
            if (!await _codex.SupportsImageGenerationAsync(
                    cancellationToken))
            {
                return new AiModuleImageGenerationResult(
                    false,
                    "The connected ChatGPT provider does not support image generation.",
                    []);
            }

            var context = JsonSerializer.Serialize(
                new
                {
                    request.SystemTitle,
                    request.SystemSummary,
                    request.Technology,
                    Modules = modules
                },
                JsonOptions);
            var prompt = await ComposePromptAsync(
                "module-images",
                context,
                cancellationToken);
            var turn = await _codex.RunTurnAsync(
                new CodexTurnRequest(
                    prompt,
                    workspace,
                    false,
                    null,
                    Math.Clamp(
                        _settings.ImageGenerationTimeoutSeconds,
                        60,
                        1_800)),
                cancellationToken);
            var generated = (turn.GeneratedImages ?? [])
                .Where(image =>
                    string.Equals(
                        image.Status,
                        "completed",
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        image.Status,
                        "succeeded",
                        StringComparison.OrdinalIgnoreCase))
                .Take(modules.Length)
                .ToArray();
            var images = new List<AiGeneratedModuleImage>(
                generated.Length);
            for (var index = 0; index < generated.Length; index++)
            {
                images.Add(await ReadGeneratedImageAsync(
                    modules[index].ModuleName,
                    generated[index],
                    workspace,
                    cancellationToken));
            }

            return images.Count == 0
                ? new AiModuleImageGenerationResult(
                    false,
                    "ChatGPT completed without generating a module image.",
                    [])
                : new AiModuleImageGenerationResult(
                    true,
                    string.Empty,
                    images);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Codex module-image generation failed.");
            return new AiModuleImageGenerationResult(
                false,
                SafeOperationError(exception, "Module image generation"),
                []);
        }
    }

    private async Task<StructuredTurn<T>> RunStructuredAsync<T>(
        string prompt,
        string workspacePath,
        bool allowWrites,
        string schemaName,
        CancellationToken cancellationToken)
    {
        var schema = await _assets.ReadSchemaAsync(
            schemaName,
            cancellationToken);
        var turn = await _codex.RunTurnAsync(
            new CodexTurnRequest(
                prompt,
                workspacePath,
                allowWrites,
                schema),
            cancellationToken);
        var payload = JsonSerializer.Deserialize<T>(
                          turn.OutputText,
                          JsonOptions)
                      ?? throw new InvalidOperationException(
                          "Codex returned an invalid structured response.");
        return new StructuredTurn<T>(payload, turn);
    }

    private async Task<string> ComposePromptAsync(
        string promptName,
        string context,
        CancellationToken cancellationToken)
    {
        var instructions = await _assets.ReadPromptAsync(
            promptName,
            cancellationToken);
        return $"{instructions.Trim()}\n\nTask context:\n{context.Trim()}";
    }

    private async Task<AiGeneratedModuleImage> ReadGeneratedImageAsync(
        string moduleName,
        CodexGeneratedImage generated,
        string workspace,
        CancellationToken cancellationToken)
    {
        byte[] bytes;
        if (!string.IsNullOrWhiteSpace(generated.SavedPath)
            && IsWithinWorkspace(generated.SavedPath, workspace))
        {
            var path = Path.GetFullPath(generated.SavedPath);
            var info = new FileInfo(path);
            if (!info.Exists)
                throw new InvalidOperationException(
                    "A generated module image could not be read.");
            if (info.Length > MaximumGeneratedImageBytes())
                throw new InvalidOperationException(
                    "A generated module image exceeded the configured size limit.");
            bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            TryDeleteGeneratedImage(path, workspace);
        }
        else
        {
            bytes = DecodeImageResult(generated.Result);
        }

        if (bytes.Length > MaximumGeneratedImageBytes())
            throw new InvalidOperationException(
                "A generated module image exceeded the configured size limit.");
        var mimeType = DetectImageMimeType(bytes)
                       ?? throw new InvalidOperationException(
                           "ChatGPT returned an unsupported image format.");
        return new AiGeneratedModuleImage(
            moduleName,
            mimeType,
            Convert.ToBase64String(bytes),
            generated.RevisedPrompt);
    }

    private int MaximumGeneratedImageBytes() =>
        Math.Clamp(
            _settings.MaxGeneratedImageBytes,
            1_000_000,
            25_000_000);

    private byte[] DecodeImageResult(string result)
    {
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException(
                "ChatGPT returned an empty module image.");
        var value = result.Trim();
        var separator = value.IndexOf(
            "base64,",
            StringComparison.OrdinalIgnoreCase);
        if (separator >= 0)
            value = value[(separator + "base64,".Length)..];
        if (value.Length > MaximumGeneratedImageBytes() * 2L)
            throw new InvalidOperationException(
                "A generated module image exceeded the configured size limit.");
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "ChatGPT returned malformed module image data.",
                exception);
        }
    }

    private static string? DetectImageMimeType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8
            && bytes[..8].SequenceEqual(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            return "image/png";
        }

        if (bytes.Length >= 3
            && bytes[0] == 255
            && bytes[1] == 216
            && bytes[2] == 255)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }

    private static void TryDeleteGeneratedImage(
        string path,
        string workspace)
    {
        try
        {
            if (IsWithinWorkspace(path, workspace))
                File.Delete(path);
        }
        catch
        {
            // Workspace retention cleanup will retry later.
        }
    }

    private static bool IsWithinWorkspace(
        string path,
        string workspace)
    {
        var relative = Path.GetRelativePath(
            Path.GetFullPath(workspace),
            Path.GetFullPath(path));
        return !relative.StartsWith("..", StringComparison.Ordinal)
               && !Path.IsPathRooted(relative);
    }

    private string ResolveWorkspace(
        string? requestedPath,
        string operation)
    {
        Directory.CreateDirectory(_workspaceRoot);
        var candidate = string.IsNullOrWhiteSpace(requestedPath)
            ? Path.Combine(_workspaceRoot, "_nonrepository", operation)
            : requestedPath;
        var fullPath = Path.GetFullPath(candidate);
        var relative = Path.GetRelativePath(_workspaceRoot, fullPath);
        if (relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException(
                "The requested AI workspace is outside the configured root.");
        }

        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    private AiBugScanResult FailedScan(
        Exception exception,
        string operation)
    {
        _logger.LogWarning(
            exception,
            "Codex {Operation} operation failed.",
            operation);
        return new AiBugScanResult(
            false,
            SafeOperationError(exception, operation),
            [],
            string.Empty);
    }

    private static AiBugFinding ToFinding(FindingPayload finding) =>
        new(
            finding.Title ?? string.Empty,
            finding.Description ?? string.Empty,
            finding.Workflow ?? string.Empty,
            finding.StepsToReproduce ?? string.Empty,
            finding.ModuleImpacted ?? string.Empty,
            finding.Severity ?? "Medium",
            finding.ScreenshotPaths ?? []);

    private static string ProjectContext(ChangeRequest project) => $"""
        Project: {project.CrNumber} - {project.Title}
        Description: {project.Description}
        Status: {project.Status}
        Stage: {project.Stage}
        Priority: {project.Priority}
        Technology: {project.TechnologyStack}
        Repository: {project.GitHubRepoUrl}
        Branch: {project.GitHubBranch}
        Product features: {string.Join(", ", project.Features.Select(feature => feature.Name))}
        Repository features: {string.Join(", ", project.RepositoryFeatures.Select(feature => feature.Name))}
        """;

    private static string BugContext(BugReport bug) => $"""
        Bug: {bug.BugNumber} - {bug.Title}
        Description: {bug.Description}
        Workflow: {bug.Workflow}
        Steps to reproduce: {bug.StepsToReproduce}
        Module: {bug.ModuleImpacted}
        Severity: {bug.Severity}
        Project: {bug.ChangeRequest?.CrNumber} - {bug.ChangeRequest?.Title}
        Technology: {bug.ChangeRequest?.TechnologyStack}
        Base branch: {bug.ChangeRequest?.GitHubBranch}
        """;

    private static string FormatFixPlan(
        FixPayload payload,
        CodexTurnResult turn)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Root cause");
        builder.AppendLine(payload.RootCause);
        builder.AppendLine();
        builder.AppendLine("Changes");
        foreach (var change in payload.Changes ?? [])
            builder.AppendLine($"- {change}");
        if (turn.ChangedFiles.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Changed files");
            foreach (var file in turn.ChangedFiles)
                builder.AppendLine($"- {file}");
        }

        builder.AppendLine();
        builder.AppendLine("Validation");
        foreach (var test in payload.Tests ?? [])
        {
            builder.AppendLine(
                $"- {test.Command}: {test.Status} — {test.Details}");
        }

        builder.AppendLine();
        builder.AppendLine("Risks");
        foreach (var risk in payload.Risks ?? [])
            builder.AppendLine($"- {risk}");
        return builder.ToString().Trim();
    }

    private static string SafeOperationError(
        Exception exception,
        string operation) =>
        exception.Message.Contains(
            "OAuth",
            StringComparison.OrdinalIgnoreCase)
            ? "Connect a ChatGPT OAuth account before running AI automation."
            : $"{operation} failed. Review the AI account and service status.";

    private sealed record StructuredTurn<T>(
        T Payload,
        CodexTurnResult Turn);

    private sealed record ScanPayload(
        IReadOnlyList<FindingPayload>? Findings);

    private sealed record FindingPayload(
        string? Title,
        string? Description,
        string? Workflow,
        string? StepsToReproduce,
        string? ModuleImpacted,
        string? Severity,
        IReadOnlyList<string>? ScreenshotPaths);

    private sealed record FixPayload(
        string? Summary,
        string? RootCause,
        IReadOnlyList<string>? Changes,
        IReadOnlyList<TestPayload>? Tests,
        IReadOnlyList<string>? Risks);

    private sealed record TestPayload(
        string? Command,
        string? Status,
        string? Details);

    private sealed record TestCasesPayload(
        IReadOnlyList<TestCasePayload>? TestCases);

    private sealed record TestCasePayload(
        string? Name,
        string? Description,
        string? Module,
        string? Category,
        string? Environment);

    private sealed record HealthPayload(
        int Score,
        string? Label,
        string? Summary,
        string? Complexity,
        IReadOnlyList<string>? Factors);
}
