using Microsoft.AspNetCore.Identity;
using System.Globalization;
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
        model.Email = NormalizeEmail(model.Email);
        model.FullName = string.IsNullOrWhiteSpace(model.FullName) ? "User" : model.FullName.Trim();
        model.Password = string.IsNullOrWhiteSpace(model.Password) ? "1" : model.Password;
        model.ConfirmPassword = string.IsNullOrWhiteSpace(model.ConfirmPassword) ? model.Password : model.ConfirmPassword;
        model.Role = string.IsNullOrWhiteSpace(model.Role) ? "Employee" : model.Role;

        var allowedRoles = new[] { "Employee", "Developer", "Tester", "Agent" };
        var role = allowedRoles.Contains(model.Role) ? model.Role : "Employee";

        if (string.IsNullOrWhiteSpace(model.Email))
        {
            return new UserRegistrationResult
            {
                Succeeded = false,
                Errors = ["Email is required."]
            };
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            return new UserRegistrationResult
            {
                Succeeded = false,
                Errors = ["Email is already registered."]
            };
        }

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

    private static string NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return string.Empty;

        return email.Trim().ToLower(CultureInfo.InvariantCulture);
    }
}
