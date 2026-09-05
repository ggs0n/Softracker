using System.Globalization;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public class OutlookCalendarSyncService : IOutlookCalendarSyncService
{
    private const string ExternalSource = "Outlook";
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public OutlookCalendarSyncService(ApplicationDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<OutlookSyncResult> SyncAsync(string userId, string icsUrl)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new OutlookSyncResult(false, "Invalid user.", 0, 0);

        if (!Uri.TryCreate(icsUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new OutlookSyncResult(false, "Please provide a valid Outlook ICS URL.", 0, 0);
        }

        string rawIcs;
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(25);
            rawIcs = await httpClient.GetStringAsync(uri);
        }
        catch (Exception ex)
        {
            return new OutlookSyncResult(false, $"Failed to download Outlook calendar feed. {ex.Message}", 0, 0);
        }

        var parsed = ParseEvents(rawIcs);
        if (parsed.Count == 0)
            return new OutlookSyncResult(false, "No events found in Outlook feed.", 0, 0);

        var added = 0;
        var updated = 0;
        var now = DateTime.UtcNow;

        foreach (var item in parsed)
        {
            var existing = await _db.CalendarEvents.FirstOrDefaultAsync(e =>
                e.CreatedById == userId &&
                e.ExternalSource == ExternalSource &&
                e.ExternalEventId == item.ExternalEventId);

            if (existing == null)
            {
                _db.CalendarEvents.Add(new CalendarEvent
                {
                    Title = item.Title,
                    Details = item.Details,
                    MeetingLink = item.MeetingLink,
                    StartAt = item.StartAtUtc,
                    EndAt = item.EndAtUtc,
                    CreatedById = userId,
                    ExternalSource = ExternalSource,
                    ExternalEventId = item.ExternalEventId,
                    LastSyncedAt = now
                });
                added++;
            }
            else
            {
                existing.Title = item.Title;
                existing.Details = item.Details;
                existing.MeetingLink = item.MeetingLink;
                existing.StartAt = item.StartAtUtc;
                existing.EndAt = item.EndAtUtc;
                existing.LastSyncedAt = now;
                updated++;
            }
        }

        await _db.SaveChangesAsync();
        return new OutlookSyncResult(true, "Outlook calendar synced successfully.", added, updated);
    }

    private static List<ParsedCalendarEvent> ParseEvents(string rawIcs)
    {
        var lines = UnfoldLines(rawIcs);
        var events = new List<ParsedCalendarEvent>();
        Dictionary<string, string>? current = null;

        foreach (var line in lines)
        {
            if (line.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (line.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (current != null && TryBuildParsedEvent(current, out var parsed))
                    events.Add(parsed);
                current = null;
                continue;
            }

            if (current == null)
                continue;

            var split = line.Split(':', 2);
            if (split.Length != 2)
                continue;

            var key = split[0];
            var value = split[1];
            var semicolonIndex = key.IndexOf(';');
            var propertyName = semicolonIndex >= 0 ? key[..semicolonIndex] : key;
            current[propertyName] = value;
        }

        return events;
    }

    private static bool TryBuildParsedEvent(
        IReadOnlyDictionary<string, string> data,
        out ParsedCalendarEvent parsed)
    {
        parsed = default;

        if (!data.TryGetValue("UID", out var uid) || string.IsNullOrWhiteSpace(uid))
            return false;
        if (!data.TryGetValue("DTSTART", out var startRaw))
            return false;

        if (!TryParseIcsDateTime(startRaw, out var startUtc))
            return false;

        DateTime? endUtc = null;
        if (data.TryGetValue("DTEND", out var endRaw) && TryParseIcsDateTime(endRaw, out var parsedEnd))
            endUtc = parsedEnd;
        if (endUtc.HasValue && endUtc.Value <= startUtc)
            endUtc = startUtc.AddHours(1);

        data.TryGetValue("SUMMARY", out var summary);
        data.TryGetValue("DESCRIPTION", out var description);
        data.TryGetValue("URL", out var url);

        parsed = new ParsedCalendarEvent(
            uid.Trim(),
            string.IsNullOrWhiteSpace(summary) ? "(Outlook Event)" : UnescapeIcsText(summary),
            string.IsNullOrWhiteSpace(description) ? null : UnescapeIcsText(description),
            string.IsNullOrWhiteSpace(url) ? null : url.Trim(),
            startUtc,
            endUtc);

        return true;
    }

    private static bool TryParseIcsDateTime(string value, out DateTime utc)
    {
        utc = default;
        var trimmed = value.Trim();

        if (DateTime.TryParseExact(
                trimmed,
                "yyyyMMdd'T'HHmmss'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var utcDateTime))
        {
            utc = utcDateTime;
            return true;
        }

        if (DateTime.TryParseExact(
                trimmed,
                "yyyyMMdd'T'HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var localDateTime))
        {
            utc = DateTime.SpecifyKind(localDateTime, DateTimeKind.Local).ToUniversalTime();
            return true;
        }

        if (DateTime.TryParseExact(
                trimmed,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dayOnly))
        {
            utc = DateTime.SpecifyKind(dayOnly, DateTimeKind.Local).ToUniversalTime();
            return true;
        }

        return false;
    }

    private static List<string> UnfoldLines(string rawIcs)
    {
        var normalized = rawIcs.Replace("\r\n", "\n").Replace('\r', '\n');
        var sourceLines = normalized.Split('\n', StringSplitOptions.None);
        var unfolded = new List<string>();

        foreach (var line in sourceLines)
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && unfolded.Count > 0)
            {
                unfolded[^1] += line[1..];
            }
            else
            {
                unfolded.Add(line.TrimEnd());
            }
        }

        return unfolded;
    }

    private static string UnescapeIcsText(string value)
    {
        return value
            .Replace("\\n", "\n")
            .Replace("\\N", "\n")
            .Replace("\\,", ",")
            .Replace("\\;", ";")
            .Replace("\\\\", "\\");
    }

    private readonly record struct ParsedCalendarEvent(
        string ExternalEventId,
        string Title,
        string? Details,
        string? MeetingLink,
        DateTime StartAtUtc,
        DateTime? EndAtUtc);
}
