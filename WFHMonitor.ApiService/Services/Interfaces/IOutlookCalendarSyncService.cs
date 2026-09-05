namespace WFHMonitor.Services.Interfaces;

public record OutlookSyncResult(bool Succeeded, string Message, int AddedCount, int UpdatedCount);

public interface IOutlookCalendarSyncService
{
    Task<OutlookSyncResult> SyncAsync(string userId, string icsUrl);
}
