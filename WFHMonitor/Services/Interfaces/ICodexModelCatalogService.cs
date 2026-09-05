namespace WFHMonitor.Services.Interfaces;

public interface ICodexModelCatalogService
{
    Task<CodexModelCatalogResult> GetModelsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}

public sealed record CodexModelCatalogResult(
    IReadOnlyList<CodexModelCatalogItem> Models,
    bool IsLive,
    string Message);

public sealed record CodexModelCatalogItem(
    string Id,
    string DisplayName,
    string DefaultReasoningEffort,
    IReadOnlyList<string> SupportedReasoningEfforts,
    bool IsDefault,
    string? UpgradeModel);
