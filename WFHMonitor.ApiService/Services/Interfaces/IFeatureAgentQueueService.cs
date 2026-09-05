namespace WFHMonitor.Services.Interfaces;

public interface IFeatureAgentQueueService
{
    Task EnqueueAsync(FeatureAgentQueueItem item, CancellationToken cancellationToken = default);
}

public sealed record FeatureAgentQueueItem(
    int FeatureId,
    string FeatureAgentId,
    string AssignedAgentUserId,
    string RequestedByUserId);
