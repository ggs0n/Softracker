using Microsoft.AspNetCore.Identity;

namespace WFHMonitor.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public string? OutlookCalendarIcsUrl { get; set; }
    public DateTime? OutlookCalendarLastSyncAt { get; set; }

    public ICollection<WorkTask> AssignedTasks { get; set; } = new List<WorkTask>();
    public ICollection<EodReport> EodReports { get; set; } = new List<EodReport>();
    public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
}
