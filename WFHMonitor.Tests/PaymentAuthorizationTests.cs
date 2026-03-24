using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using WFHMonitor.Controllers;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Tests;

public class PaymentAuthorizationTests
{
    [Fact]
    public void PaymentController_RequiresAuthentication()
    {
        var authorize = typeof(PaymentController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.True(string.IsNullOrWhiteSpace(authorize!.Roles));
    }

    [Fact]
    public async Task Success_RejectsCheckoutSessionForDifferentUser()
    {
        var user = new ApplicationUser
        {
            Id = "user-123",
            Email = "user@example.com",
            UserName = "user@example.com"
        };

        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(user);

        var stripe = new Mock<IStripeBillingService>();
        stripe.Setup(x => x.GetCheckoutSessionAsync("sess_other", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new StripeCheckoutSessionInfo("sess_other", "complete", "paid", "cus_1", "sub_1", "other-user"), string.Empty));

        var controller = new PaymentController(userManager.Object, null!, stripe.Object, Mock.Of<ISystemSettingsService>());
        AttachHttpContext(controller);

        var result = await controller.Success("sess_other");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Checkout session does not match the current user.", controller.TempData["Error"]);
        userManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    private static void AttachHttpContext(Controller controller)
    {
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
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
