namespace WFHMonitor.Services.Interfaces;

public interface IGitHubService
{
    Task<List<GitHubCommit>> GetCommitsAsync(string owner, string repo, string branch, int count = 10);
    Task<List<string>> GetRepoTreeAsync(string owner, string repo, string branch);
}
