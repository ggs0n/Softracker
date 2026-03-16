using Microsoft.AspNetCore.Identity;

namespace WFHMonitor.Models;

public enum SubscriptionPlan
{
    Free = 0,
    Pro = 1
}

public enum OrganizationTeam
{
    Unassigned = 0,
    Team1 = 1,
    Team2 = 2
}

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public SubscriptionPlan SubscriptionPlan { get; set; } = SubscriptionPlan.Free;
    public bool IsProSubscriptionActive { get; set; } = false;
    public DateTime? ProSubscribedAt { get; set; }
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public OrganizationTeam OrganizationTeam { get; set; } = OrganizationTeam.Unassigned;
    public string? OutlookCalendarIcsUrl { get; set; }
    public DateTime? OutlookCalendarLastSyncAt { get; set; }

    public ICollection<WorkTask> AssignedTasks { get; set; } = new List<WorkTask>();
    public ICollection<EodReport> EodReports { get; set; } = new List<EodReport>();
    public ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();
}
