using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface IProjectMonitoringService
{
    Task<MonitorDashboardViewModel> BuildDashboardAsync(bool isAdmin, int? orgTeamId, CancellationToken cancellationToken = default);
}
