using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;

namespace WFHMonitor.Services;

/// <summary>
/// Stamps LastActivityAt on authenticated users. Throttled to once per 5 minutes per user.
/// Uses raw SQL to avoid loading the full user entity.
/// </summary>
public sealed class UserActivityMiddleware
{
    private static readonly ConcurrentDictionary<string, DateTime> LastStamped = new();
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromMinutes(5);
    private const int MaxDictionarySize = 500;

    private readonly RequestDelegate _next;

    public UserActivityMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrEmpty(userId))
        {
            var now = DateTime.UtcNow;
            var shouldUpdate = !LastStamped.TryGetValue(userId, out var lastStamp)
                               || (now - lastStamp) >= ThrottleInterval;

            if (shouldUpdate)
            {
                LastStamped[userId] = now;
                EvictStaleEntries(now);

                var db = context.RequestServices.GetRequiredService<ApplicationDbContext>();
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE AspNetUsers SET LastActivityAt = {now} WHERE Id = {userId}");
            }
        }

        await _next(context);
    }

    private static void EvictStaleEntries(DateTime now)
    {
        if (LastStamped.Count <= MaxDictionarySize) return;

        foreach (var kvp in LastStamped)
        {
            if ((now - kvp.Value) > TimeSpan.FromMinutes(10))
                LastStamped.TryRemove(kvp.Key, out _);
        }
    }
}
