namespace WFHMonitor.Services.Interfaces;

public interface IProjectBugScanQueueService
{
    Task EnqueueAsync(ProjectBugScanQueueItem item, CancellationToken cancellationToken = default);
}

public sealed record ProjectBugScanQueueItem(
    int ProjectId,
    string ScanAgentId,
    string RequestedByUserId);
