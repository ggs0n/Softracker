namespace WFHMonitor.Services.Interfaces;

public interface IGitHubService
{
    Task<List<GitHubCommit>> GetCommitsAsync(string owner, string repo, string branch, int count = 10);
    Task<List<string>> GetRepoTreeAsync(string owner, string repo, string branch);
    Task<GitHubRepositoryInfo> GetRepositoryInfoAsync(string owner, string repo);
    Task<string?> GetReadmeContentAsync(string owner, string repo);
    Task<Dictionary<string, long>> GetRepositoryLanguagesAsync(string owner, string repo);
}
