namespace WFHMonitor.Services.Interfaces;

public interface IGitHubService
{
    Task<List<GitHubCommit>> GetCommitsAsync(string owner, string repo, string branch, int count = 10);
}
