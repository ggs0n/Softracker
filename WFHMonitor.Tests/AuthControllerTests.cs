using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using WFHMonitor.Controllers;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Tests;

public class AuthControllerTests
{
    [Fact]
    public async Task Logout_SignsOut_DeletesCookies_AndRedirectsToLogin()
    {
        var signInManager = CreateSignInManagerMock();
        var controller = new AuthController(
            CreateUserManagerMock().Object,
            signInManager.Object,
            CreateRoleManagerMock().Object,
            Mock.Of<IJwtTokenService>(),
            Mock.Of<IUserRegistrationService>());

        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.TempData = tempData;

        var result = await controller.Logout();

        signInManager.Verify(x => x.SignOutAsync(), Times.Once);

        var setCookieHeaders = httpContext.Response.Headers["Set-Cookie"].ToArray();
        Assert.Contains(setCookieHeaders, header => header is not null
            && header.Contains("jwt_token=", StringComparison.Ordinal)
            && header.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(setCookieHeaders, header => header is not null
            && header.Contains("new_user_jwt_token=", StringComparison.Ordinal)
            && header.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("You have been signed out.", controller.TempData["Info"]);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
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

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManagerMock()
    {
        return new Mock<SignInManager<ApplicationUser>>(
            CreateUserManagerMock().Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null!,
            null!,
            null!,
            null!);
    }

    private static Mock<RoleManager<IdentityRole>> CreateRoleManagerMock()
    {
        var store = new Mock<IRoleStore<IdentityRole>>();
        return new Mock<RoleManager<IdentityRole>>(
            store.Object,
            null!,
            null!,
            null!,
            null!);
    }
}
