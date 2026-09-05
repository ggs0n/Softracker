namespace WFHMonitor.Services.Interfaces;

public interface ICodeReadinessScanQueueService
{
    Task EnqueueAsync(CodeReadinessScanQueueItem item, CancellationToken cancellationToken = default);
}

public sealed record CodeReadinessScanQueueItem(
    int ProjectId,
    string RequestedByUserId,
    string? ScanAgentId);
