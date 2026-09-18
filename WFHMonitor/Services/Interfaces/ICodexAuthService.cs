namespace WFHMonitor.Services.Interfaces;

public interface ICodexAuthService
{
    Task<CodexAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<CodexLoginStartResult> StartLoginAsync(CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(CancellationToken cancellationToken = default);
}

public sealed record CodexAuthStatus(
    bool IsAvailable,
    bool IsAuthenticated,
    string Message,
    bool IsChatGptLogin = false);

public sealed record CodexLoginStartResult(
    bool Started,
    string Message);
