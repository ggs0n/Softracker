using System.Text.Json.Nodes;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.AutomationService.Services;

public interface ICodexAppServerClient
{
    string Model { get; }

    Task<AiAccountStatus> GetAccountStatusAsync(
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

    Task<bool> SupportsImageGenerationAsync(
        CancellationToken cancellationToken = default);

    Task<CodexTurnResult> RunTurnAsync(
        CodexTurnRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CodexTurnRequest(
    string Prompt,
    string WorkingDirectory,
    bool AllowWrites,
    JsonNode? OutputSchema = null,
    int? TimeoutSeconds = null);

public sealed record CodexTurnResult(
    string OutputText,
    IReadOnlyList<CodexCommandExecution> Commands,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<CodexGeneratedImage>? GeneratedImages = null);

public sealed record CodexGeneratedImage(
    string Status,
    string Result,
    string? RevisedPrompt,
    string? SavedPath);

public sealed record CodexCommandExecution(
    string Command,
    int? ExitCode,
    string? Output);
