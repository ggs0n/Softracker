namespace WFHMonitor.Services.Interfaces;

public interface IQaCodexQueueService
{
    Task EnqueueAsync(QaCodexQueueItem item, CancellationToken cancellationToken = default);
}

public enum QaCodexQueueOperation
{
    ScanAndGenerate,
    AutoGenerate,
    ScanModule
}

public sealed record QaCodexQueueItem(
    QaCodexQueueOperation Operation,
    string RequestedByUserId,
    int? ProjectId = null,
    int? TestCaseId = null,
    string? ScanAgentId = null,
    bool UseSecurityPrompt = false);
