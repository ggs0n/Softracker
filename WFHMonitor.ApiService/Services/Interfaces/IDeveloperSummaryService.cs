namespace WFHMonitor.Services.Interfaces;

public interface IDeveloperSummaryService
{
    Task<WFHMonitor.ViewModels.DeveloperSummaryViewModel> BuildSummaryAsync(string userId);
}
