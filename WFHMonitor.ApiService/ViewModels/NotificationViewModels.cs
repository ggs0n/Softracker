namespace WFHMonitor.ViewModels;

public class NotificationItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationBellViewModel
{
    public int UnreadCount { get; set; }
    public List<NotificationItemViewModel> Items { get; set; } = new();
}

public class NotificationDropdownViewModel
{
    public NotificationBellViewModel Bell { get; set; } = new();
    public string ReturnUrl { get; set; } = "/";
}
