using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationBellViewModel> GetBellAsync(string userId, int maxItems = 8)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new NotificationBellViewModel();

        var unreadCount = await _db.UserNotifications
            .CountAsync(n => n.RecipientId == userId && !n.IsRead);

        var items = await _db.UserNotifications
            .Where(n => n.RecipientId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(maxItems)
            .Select(n => new NotificationItemViewModel
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                LinkUrl = n.LinkUrl,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return new NotificationBellViewModel
        {
            UnreadCount = unreadCount,
            Items = items
        };
    }

    public async Task CreateAsync(string recipientId, string title, string message, string? linkUrl)
    {
        if (string.IsNullOrWhiteSpace(recipientId))
            return;

        var notification = new UserNotification
        {
            RecipientId = recipientId,
            Title = title.Trim(),
            Message = message.Trim(),
            LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim(),
            IsRead = false
        };

        _db.UserNotifications.Add(notification);
        await _db.SaveChangesAsync();
    }

    public async Task MarkAsReadAsync(int notificationId, string userId)
    {
        var notification = await _db.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientId == userId);

        if (notification == null || notification.IsRead)
            return;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var unread = await _db.UserNotifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ToListAsync();

        if (unread.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await _db.SaveChangesAsync();
    }
}
