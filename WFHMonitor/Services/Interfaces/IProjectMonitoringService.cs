using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface IProjectMonitoringService
{
    Task<MonitorDashboardViewModel> BuildDashboardAsync(CancellationToken cancellationToken = default);
}
