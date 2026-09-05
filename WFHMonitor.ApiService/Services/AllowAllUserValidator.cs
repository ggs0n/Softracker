using Microsoft.AspNetCore.Identity;
using WFHMonitor.Models;

namespace WFHMonitor.Services;

public sealed class AllowAllUserValidator : IUserValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        return Task.FromResult(IdentityResult.Success);
    }
}
