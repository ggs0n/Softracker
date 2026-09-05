namespace WFHMonitor.Services.Interfaces;

public interface IQaAiAutomationQueueService
{
    Task EnqueueAsync(
        QaAiAutomationQueueItem item,
        CancellationToken cancellationToken = default);
}

public enum QaAiAutomationQueueOperation
{
    ScanAndGenerate,
    AutoGenerate,
    ScanModule
}

public sealed record QaAiAutomationQueueItem(
    QaAiAutomationQueueOperation Operation,
    string RequestedByUserId,
    int? ProjectId = null,
    int? TestCaseId = null,
    string? ScanAgentId = null,
    bool UseSecurityPrompt = false);
