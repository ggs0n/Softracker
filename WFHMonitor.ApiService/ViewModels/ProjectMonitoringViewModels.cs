namespace WFHMonitor.ViewModels;

public enum MonitorSignalState
{
    Healthy,
    Unhealthy,
    Unknown
}

public class MonitorStatusViewModel
{
    public MonitorSignalState State { get; set; } = MonitorSignalState.Unknown;
    public string Label { get; set; } = "Unknown";
    public string Details { get; set; } = "No status available.";

    public string DotClass => State switch
    {
        MonitorSignalState.Healthy => "is-green",
        MonitorSignalState.Unhealthy => "is-red",
        _ => "is-gray"
    };
}

public class ProjectMonitorCardViewModel
{
    public int ProjectId { get; set; }
    public string CrNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string AwsRegion { get; set; } = "Not configured";

    public MonitorStatusViewModel AwsHealth { get; set; } = new();

    public long? AnalyticsViewsLast30Days { get; set; }
    public string AnalyticsLabel { get; set; } = "Last 30 days";
    public string AnalyticsMessage { get; set; } = "Google Analytics is not configured for this project.";
}

public class StripePaymentHistoryViewModel
{
    public string Id { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class MonitorDashboardViewModel
{
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public List<ProjectMonitorCardViewModel> Projects { get; set; } = new();
    public List<StripePaymentHistoryViewModel> StripePayments { get; set; } = new();
    public string StripeMessage { get; set; } = "Stripe is not configured.";
}
