using WFHMonitor.Models;

namespace WFHMonitor.Services.Interfaces;

public interface IJwtTokenService
{
    Task<string> GenerateTokenAsync(ApplicationUser user);
}
