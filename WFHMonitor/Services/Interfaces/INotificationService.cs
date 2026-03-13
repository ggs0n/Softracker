using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface INotificationService
{
    Task<NotificationBellViewModel> GetBellAsync(string userId, int maxItems = 8);
    Task CreateAsync(string recipientId, string title, string message, string? linkUrl);
    Task MarkAsReadAsync(int notificationId, string userId);
    Task MarkAllAsReadAsync(string userId);
}
