using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class AdminDashboardViewModel
{
    public DateTime Today { get; set; } = DateTime.UtcNow.Date;
    public List<WorkTask> TasksDoneToday { get; set; } = new();
    public List<WorkTask> BlockedTasks { get; set; } = new();
    public int TotalEmployees { get; set; }
    public int TasksDoneTodayCount => TasksDoneToday.Count;
}
