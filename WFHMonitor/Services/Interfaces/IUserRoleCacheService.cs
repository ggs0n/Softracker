using WFHMonitor.Models;

namespace WFHMonitor.Services.Interfaces;

public interface IUserRoleCacheService
{
    Task<IReadOnlyList<ApplicationUser>> GetUsersInRoleAsync(string roleName);
    Task<IReadOnlyDictionary<string, IReadOnlyList<ApplicationUser>>> GetAllUsersByRoleAsync();
    void InvalidateCache();
}
