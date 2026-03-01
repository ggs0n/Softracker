using Microsoft.AspNetCore.Identity;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public class UserRegistrationService : IUserRegistrationService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserRegistrationService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UserRegistrationResult> RegisterAsync(RegisterViewModel model)
    {
        model.Email = (model.Email ?? string.Empty).Trim();
        model.FullName = string.IsNullOrWhiteSpace(model.FullName) ? "User" : model.FullName.Trim();
        model.Password = string.IsNullOrWhiteSpace(model.Password) ? "1" : model.Password;
        model.ConfirmPassword = string.IsNullOrWhiteSpace(model.ConfirmPassword) ? model.Password : model.ConfirmPassword;
        model.Role = string.IsNullOrWhiteSpace(model.Role) ? "Employee" : model.Role;

        var allowedRoles = new[] { "Employee", "Developer", "Tester", "Agent" };
        var role = allowedRoles.Contains(model.Role) ? model.Role : "Employee";

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, model.Password);
        if (!createResult.Succeeded)
        {
            return new UserRegistrationResult
            {
                Succeeded = false,
                Errors = createResult.Errors.Select(e => e.Description).ToList()
            };
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, role);
        if (!addRoleResult.Succeeded)
        {
            return new UserRegistrationResult
            {
                Succeeded = false,
                Errors = addRoleResult.Errors.Select(e => e.Description).ToList()
            };
        }

        return new UserRegistrationResult
        {
            Succeeded = true,
            Role = role,
            FullName = model.FullName,
            User = user
        };
    }
}
