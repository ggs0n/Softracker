using Microsoft.AspNetCore.Identity;
using WFHMonitor.Models;

namespace WFHMonitor.Services;

public sealed class AllowAllPasswordValidator : IPasswordValidator<ApplicationUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        return Task.FromResult(IdentityResult.Success);
    }
}
