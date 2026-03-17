namespace WFHMonitor.Services.Interfaces;

public interface IBugFixQueueService
{
    Task EnqueueAsync(BugFixQueueItem item, CancellationToken cancellationToken = default);
}

public sealed record BugFixQueueItem(
    int BugId,
    string FixAgentId,
    string AssignedAgentUserId,
    string RequestedByUserId);
