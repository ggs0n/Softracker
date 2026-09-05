namespace WFHMonitor.Services.Interfaces;

public interface IRepositorySnapshotService
{
    Task<RepositorySnapshot> CreateAsync(
        int projectId,
        string owner,
        string repository,
        string branch,
        string requestedByUserId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(RepositorySnapshot snapshot);
}

public sealed record RepositorySnapshot(
    string RootPath,
    string CommitSha,
    string SnapshotDirectory);
