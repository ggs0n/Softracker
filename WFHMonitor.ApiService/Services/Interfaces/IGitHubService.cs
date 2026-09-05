namespace WFHMonitor.Services.Interfaces;

public interface IGitHubService
{
    Task<List<GitHubCommit>> GetCommitsAsync(string owner, string repo, string branch, int count = 10);
    Task<List<string>> GetRepoTreeAsync(string owner, string repo, string branch);
    Task<GitHubRepositoryInfo> GetRepositoryInfoAsync(string owner, string repo);
    Task<string?> GetReadmeContentAsync(string owner, string repo);
    Task<Dictionary<string, long>> GetRepositoryLanguagesAsync(string owner, string repo);
    Task<List<GitHubConnectedRepository>> GetCurrentUserRepositoriesAsync(int maxCount = 200);
    Task<GitHubPullRequestResult> CreateDraftPullRequestAsync(
        string owner,
        string repository,
        string title,
        string headBranch,
        string baseBranch,
        string body,
        string requestingUserId,
        CancellationToken cancellationToken = default);
}

public sealed record GitHubPullRequestResult(
    int Number,
    string Url);

public class GitHubOAuthCallbackResult
{
    public bool Succeeded { get; set; }
    public string ReturnUrl { get; set; } = "/";
    public string? ErrorMessage { get; set; }
}

public interface IGitHubOAuthService
{
    bool IsOAuthConfigured();
    Task<bool> IsConnectedAsync(string userId);
    Task<string?> GetCurrentUserAccessTokenAsync();
    Task<string?> GetAccessTokenForUserAsync(string userId);
    Task<string> BuildAuthorizeUrlAsync(string userId, string? returnUrl);
    Task<GitHubOAuthCallbackResult> CompleteAuthorizationAsync(string code, string state, string currentUserId);
    Task DisconnectAsync(string userId);
}
