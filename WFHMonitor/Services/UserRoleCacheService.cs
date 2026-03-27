using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public class UserRoleCacheService : IUserRoleCacheService
{
    private const string CacheKey = "users:byRole";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public UserRoleCacheService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlyList<ApplicationUser>> GetUsersInRoleAsync(string roleName)
    {
        var all = await GetAllUsersByRoleAsync();
        return all.TryGetValue(roleName, out var users) ? users : Array.Empty<ApplicationUser>();
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<ApplicationUser>>> GetAllUsersByRoleAsync()
    {
        return (await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            var userRoles = await _db.Users
                .Join(_db.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { User = u, ur.RoleId })
                .Join(_db.Roles, x => x.RoleId, r => r.Id, (x, r) => new { x.User, RoleName = r.Name! })
                .AsNoTracking()
                .ToListAsync();

            var result = userRoles
                .GroupBy(x => x.RoleName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<ApplicationUser>)g.Select(x => x.User).Distinct().ToList(),
                    StringComparer.OrdinalIgnoreCase);

            return (IReadOnlyDictionary<string, IReadOnlyList<ApplicationUser>>)result;
        }))!;
    }

    public void InvalidateCache() => _cache.Remove(CacheKey);
}
