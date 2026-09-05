using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface IUserRegistrationService
{
    Task<UserRegistrationResult> RegisterAsync(RegisterViewModel model);
}

public class UserRegistrationResult
{
    public bool Succeeded { get; set; }
    public string Role { get; set; } = "Employee";
    public string FullName { get; set; } = "User";
    public WFHMonitor.Models.ApplicationUser? User { get; set; }
    public List<string> Errors { get; set; } = new();
}
