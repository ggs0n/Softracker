namespace WFHMonitor.Services.Interfaces;

public interface IQaOpenClawQueueService
{
    Task EnqueueAsync(QaOpenClawQueueItem item, CancellationToken cancellationToken = default);
}

public enum QaOpenClawQueueOperation
{
    ScanAndGenerate,
    AutoGenerate,
    ScanModule
}

public sealed record QaOpenClawQueueItem(
    QaOpenClawQueueOperation Operation,
    string RequestedByUserId,
    int? ProjectId = null,
    int? TestCaseId = null,
    string? ScanAgentId = null,
    bool UseSecurityPrompt = false);
