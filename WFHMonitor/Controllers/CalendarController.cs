using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Globalization;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize]
public class CalendarController : Controller
{
    private static readonly string[] LocalDateTimeFormats =
    [
        "yyyy-MM-ddTHH:mm",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff"
    ];

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOutlookCalendarSyncService _outlookSyncService;

    public CalendarController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IOutlookCalendarSyncService outlookSyncService)
    {
        _db = db;
        _userManager = userManager;
        _outlookSyncService = outlookSyncService;
    }

    [HttpGet]
    public async Task<IActionResult> Events(int year, int month)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            return BadRequest("Invalid year or month.");

        var userId = _userManager.GetUserId(User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null && !string.IsNullOrWhiteSpace(user.OutlookCalendarIcsUrl))
            {
                var shouldSync = !user.OutlookCalendarLastSyncAt.HasValue ||
                                 user.OutlookCalendarLastSyncAt.Value < DateTime.UtcNow.AddMinutes(-5);
                if (shouldSync)
                {
                    var sync = await _outlookSyncService.SyncAsync(userId, user.OutlookCalendarIcsUrl);
                    if (sync.Succeeded)
                    {
                        user.OutlookCalendarLastSyncAt = DateTime.UtcNow;
                        await _userManager.UpdateAsync(user);
                    }
                }
            }
        }

        var monthStartUtc = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEndUtc = monthStartUtc.AddMonths(1);

        var events = await _db.CalendarEvents
            .Where(e => e.StartAt < monthEndUtc && (e.EndAt == null || e.EndAt >= monthStartUtc))
            .OrderBy(e => e.StartAt)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Details,
                e.MeetingLink,
                e.StartAt,
                e.EndAt
            })
            .AsNoTracking()
            .ToListAsync();

        return Json(events);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CalendarEventCreateViewModel model, string? returnUrl)
    {
        var hasStart = TryParseLocalDateTime(model.StartAt, out var parsedStart);
        DateTime? parsedEnd = null;

        if (!hasStart)
            ModelState.AddModelError(nameof(model.StartAt), "Please provide a valid start date/time.");

        if (!string.IsNullOrWhiteSpace(model.EndAt))
        {
            if (!TryParseLocalDateTime(model.EndAt, out var end))
                ModelState.AddModelError(nameof(model.EndAt), "Please provide a valid end date/time.");
            else
                parsedEnd = end;
        }

        if (hasStart && parsedEnd.HasValue && parsedEnd.Value < parsedStart)
            ModelState.AddModelError(nameof(model.EndAt), "End date/time must be after start date/time.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Unable to create event. Please check your inputs.";
            return RedirectToLocal(returnUrl);
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var calendarEvent = new CalendarEvent
        {
            Title = model.Title.Trim(),
            Details = string.IsNullOrWhiteSpace(model.Details) ? null : model.Details.Trim(),
            MeetingLink = string.IsNullOrWhiteSpace(model.MeetingLink) ? null : model.MeetingLink.Trim(),
            StartAt = DateTime.SpecifyKind(parsedStart, DateTimeKind.Local).ToUniversalTime(),
            EndAt = parsedEnd.HasValue
                ? DateTime.SpecifyKind(parsedEnd.Value, DateTimeKind.Local).ToUniversalTime()
                : null,
            CreatedById = userId
        };

        _db.CalendarEvents.Add(calendarEvent);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Calendar event added.";
        return RedirectToLocal(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConnectOutlook(string icsUrl, string? returnUrl)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        if (string.IsNullOrWhiteSpace(icsUrl))
        {
            TempData["Error"] = "Please provide your Outlook ICS URL.";
            return RedirectToLocal(returnUrl);
        }

        if (!Uri.TryCreate(icsUrl.Trim(), UriKind.Absolute, out var parsedUri) ||
            (parsedUri.Scheme != Uri.UriSchemeHttp && parsedUri.Scheme != Uri.UriSchemeHttps))
        {
            TempData["Error"] = "Please enter a valid ICS URL (http/https).";
            return RedirectToLocal(returnUrl);
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Forbid();

        user.OutlookCalendarIcsUrl = parsedUri.ToString();
        await _userManager.UpdateAsync(user);

        TempData["Success"] = "Outlook calendar URL connected. Click Sync Outlook to import events.";
        return RedirectToLocal(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncOutlook(string? returnUrl)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Forbid();

        if (string.IsNullOrWhiteSpace(user.OutlookCalendarIcsUrl))
        {
            TempData["Error"] = "Connect your Outlook ICS URL first.";
            return RedirectToLocal(returnUrl);
        }

        var sync = await _outlookSyncService.SyncAsync(userId, user.OutlookCalendarIcsUrl);
        if (!sync.Succeeded)
        {
            TempData["Error"] = sync.Message;
            return RedirectToLocal(returnUrl);
        }

        user.OutlookCalendarLastSyncAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        TempData["Success"] = $"{sync.Message} Added: {sync.AddedCount}, Updated: {sync.UpdatedCount}.";
        return RedirectToLocal(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisconnectOutlook(string? returnUrl)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Forbid();

        user.OutlookCalendarIcsUrl = null;
        user.OutlookCalendarLastSyncAt = null;
        await _userManager.UpdateAsync(user);

        TempData["Info"] = "Outlook calendar connection removed.";
        return RedirectToLocal(returnUrl);
    }

    [HttpGet]
    public async Task<IActionResult> FeedIcs()
    {
        var events = await _db.CalendarEvents
            .OrderBy(e => e.StartAt)
            .AsNoTracking()
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Softracker//Calendar//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine("X-WR-CALNAME:Softracker Team Calendar");

        foreach (var calendarEvent in events)
            AppendIcsEvent(sb, calendarEvent);

        sb.AppendLine("END:VCALENDAR");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/calendar; charset=utf-8", "softracker-calendar.ics");
    }

    [HttpGet]
    public async Task<IActionResult> EventIcs(int id)
    {
        var calendarEvent = await _db.CalendarEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (calendarEvent == null)
            return NotFound();

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Softracker//Calendar//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        AppendIcsEvent(sb, calendarEvent);
        sb.AppendLine("END:VCALENDAR");

        var safeTitle = string.Join("-", calendarEvent.Title
            .Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
            .Trim();
        if (string.IsNullOrWhiteSpace(safeTitle))
            safeTitle = $"event-{calendarEvent.Id}";

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/calendar; charset=utf-8", $"softracker-{safeTitle}.ics");
    }

    private static void AppendIcsEvent(StringBuilder sb, CalendarEvent calendarEvent)
    {
        var startUtc = calendarEvent.StartAt.Kind == DateTimeKind.Utc
            ? calendarEvent.StartAt
            : DateTime.SpecifyKind(calendarEvent.StartAt, DateTimeKind.Utc);

        var endUtcRaw = calendarEvent.EndAt ?? calendarEvent.StartAt.AddHours(1);
        var endUtc = endUtcRaw.Kind == DateTimeKind.Utc
            ? endUtcRaw
            : DateTime.SpecifyKind(endUtcRaw, DateTimeKind.Utc);

        if (endUtc <= startUtc)
            endUtc = startUtc.AddHours(1);

        var description = calendarEvent.Details ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(calendarEvent.MeetingLink))
            description = string.IsNullOrWhiteSpace(description)
                ? $"Meeting Link: {calendarEvent.MeetingLink}"
                : $"{description}\\nMeeting Link: {calendarEvent.MeetingLink}";

        sb.AppendLine("BEGIN:VEVENT");
        sb.AppendLine($"UID:softracker-calendar-{calendarEvent.Id}@local");
        sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}");
        sb.AppendLine($"DTSTART:{startUtc:yyyyMMdd'T'HHmmss'Z'}");
        sb.AppendLine($"DTEND:{endUtc:yyyyMMdd'T'HHmmss'Z'}");
        sb.AppendLine($"SUMMARY:{EscapeIcsText(calendarEvent.Title)}");
        sb.AppendLine($"DESCRIPTION:{EscapeIcsText(description)}");
        if (!string.IsNullOrWhiteSpace(calendarEvent.MeetingLink))
            sb.AppendLine($"URL:{EscapeIcsText(calendarEvent.MeetingLink)}");
        sb.AppendLine("END:VEVENT");
    }

    private static string EscapeIcsText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace(";", "\\;")
            .Replace(",", "\\,")
            .Replace("\r\n", "\\n")
            .Replace("\n", "\\n")
            .Replace("\r", "\\n");
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction("Index", "TaskBoard");
    }

    private static bool TryParseLocalDateTime(string? value, out DateTime parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (DateTime.TryParseExact(
                value.Trim(),
                LocalDateTimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsed))
        {
            return true;
        }

        return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsed);
    }
}
