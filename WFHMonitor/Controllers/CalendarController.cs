using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WFHMonitor.Data;
using WFHMonitor.Models;
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

    public CalendarController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Events(int year, int month)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            return BadRequest("Invalid year or month.");

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
