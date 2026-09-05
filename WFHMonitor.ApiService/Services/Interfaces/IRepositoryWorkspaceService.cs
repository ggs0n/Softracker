using WFHMonitor.Models;

namespace WFHMonitor.Services.Interfaces;

public interface IRepositoryWorkspaceService
{
    Task<RepositoryWorkspace> PrepareAsync(
        ChangeRequest project,
        string requestingUserId,
        string workItemKey,
        CancellationToken cancellationToken = default);

    Task<RepositoryWorkspace> PrepareReadOnlyAsync(
        ChangeRequest project,
        string requestingUserId,
        string workItemKey,
        CancellationToken cancellationToken = default);

    Task<RepositoryPushResult> CommitAndPushAsync(
        RepositoryWorkspace workspace,
        string commitMessage,
        CancellationToken cancellationToken = default);
}

public sealed record RepositoryWorkspace(
    string JobDirectory,
    string RepositoryDirectory,
    string Owner,
    string Repository,
    string BaseBranch,
    string WorkBranch,
    string RequestingUserId);

public sealed record RepositoryPushResult(
    bool HasChanges,
    string Summary);
