using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using WFHMonitor.Models;

namespace WFHMonitor.Services;

/// <summary>
/// Stamps LastActivityAt on authenticated users. Throttled to once per 2 minutes per user
/// to avoid excessive DB writes on every request.
/// </summary>
public sealed class UserActivityMiddleware
{
    private static readonly ConcurrentDictionary<string, DateTime> LastStamped = new();
    private static readonly TimeSpan ThrottleInterval = TimeSpan.FromMinutes(2);

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

                var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    user.LastActivityAt = now;
                    await userManager.UpdateAsync(user);
                }
            }
        }

        await _next(context);
    }
}
