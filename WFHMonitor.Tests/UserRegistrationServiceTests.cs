using Microsoft.AspNetCore.Identity;
using Moq;
using WFHMonitor.Models;
using WFHMonitor.Services;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Tests;

public class UserRegistrationServiceTests
{
    [Fact]
    public async Task RegisterAsync_NormalizesEmailBeforeCreate()
    {
        var userManager = CreateUserManagerMock();
        userManager
            .Setup(x => x.FindByEmailAsync("person@example.com"))
            .ReturnsAsync((ApplicationUser?)null);
        userManager
            .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager
            .Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Employee"))
            .ReturnsAsync(IdentityResult.Success);

        var service = new UserRegistrationService(userManager.Object);
        var model = new RegisterViewModel
        {
            Email = "  Person@Example.com  ",
            FullName = "  Person Example  ",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Role = "Employee"
        };

        var result = await service.RegisterAsync(model);

        Assert.True(result.Succeeded);
        Assert.Equal("person@example.com", model.Email);
        userManager.Verify(x => x.FindByEmailAsync("person@example.com"), Times.Once);
        userManager.Verify(x => x.CreateAsync(
            It.Is<ApplicationUser>(u => u.Email == "person@example.com" && u.UserName == "person@example.com"),
            "Password123!"), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_RejectsDuplicateEmailAfterNormalization()
    {
        var userManager = CreateUserManagerMock();
        userManager
            .Setup(x => x.FindByEmailAsync("person@example.com"))
            .ReturnsAsync(new ApplicationUser { Email = "person@example.com", UserName = "person@example.com" });

        var service = new UserRegistrationService(userManager.Object);
        var model = new RegisterViewModel
        {
            Email = " Person@Example.com ",
            FullName = "Person Example",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Role = "Employee"
        };

        var result = await service.RegisterAsync(model);

        Assert.False(result.Succeeded);
        Assert.Contains("Email is already registered.", result.Errors);
        userManager.Verify(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        userManager.Verify(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
    }
}
