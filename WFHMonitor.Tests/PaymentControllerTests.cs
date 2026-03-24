using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using WFHMonitor.Controllers;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Tests;

public class PaymentControllerTests
{
    [Fact]
    public async Task ChoosePlan_ProWithoutActiveSubscription_DoesNotPersistProState()
    {
        var user = CreateUser();
        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(user);

        var controller = CreateController(userManager.Object, Mock.Of<IStripeBillingService>(), CreateDbContext());
        AttachHttpContext(controller);

        var result = await controller.ChoosePlan(SubscriptionPlan.Pro, null);

        Assert.Equal(SubscriptionPlan.Free, user.SubscriptionPlan);
        Assert.False(user.IsProSubscriptionActive);
        userManager.Verify(x => x.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
        Assert.Equal("Pro plan selected. Complete Stripe payment to activate unlimited access.", controller.TempData["Info"]);
        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task Cancel_InactivePendingProSelection_RevertsToFree()
    {
        var user = CreateUser();
        user.SubscriptionPlan = SubscriptionPlan.Pro;
        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(user);
        userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var controller = CreateController(userManager.Object, Mock.Of<IStripeBillingService>(), CreateDbContext());
        AttachHttpContext(controller);

        var result = await controller.Cancel();

        Assert.Equal(SubscriptionPlan.Free, user.SubscriptionPlan);
        Assert.False(user.IsProSubscriptionActive);
        userManager.Verify(x => x.UpdateAsync(user), Times.Once);
        Assert.Equal("Stripe checkout was canceled. You can resume payment anytime.", controller.TempData["Info"]);
        Assert.IsType<RedirectToActionResult>(result);
    }

    [Fact]
    public async Task Success_PaidCheckout_ActivatesProSubscription()
    {
        var user = CreateUser();
        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(user);
        userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var stripe = new Mock<IStripeBillingService>();
        stripe.SetupGet(x => x.IsConfigured).Returns(true);
        stripe.Setup(x => x.GetCheckoutSessionAsync("sess_ok", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new StripeCheckoutSessionInfo("sess_ok", "complete", "paid", "cus_123", "sub_123", user.Id), string.Empty));

        var controller = CreateController(userManager.Object, stripe.Object, CreateDbContext());
        AttachHttpContext(controller);

        var result = await controller.Success("sess_ok");

        Assert.Equal(SubscriptionPlan.Pro, user.SubscriptionPlan);
        Assert.True(user.IsProSubscriptionActive);
        Assert.Equal("sub_123", user.StripeSubscriptionId);
        Assert.Equal("Payment successful. Pro subscription is now active.", controller.TempData["Success"]);
        Assert.IsType<RedirectToActionResult>(result);
    }

    private static PaymentController CreateController(UserManager<ApplicationUser> userManager, IStripeBillingService stripe, ApplicationDbContext db)
    {
        return new PaymentController(userManager, db, stripe, Mock.Of<ISystemSettingsService>());
    }

    private static void AttachHttpContext(Controller controller)
    {
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    private static ApplicationUser CreateUser()
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = "user@example.com",
            Email = "user@example.com",
            SubscriptionPlan = SubscriptionPlan.Free,
            IsProSubscriptionActive = false
        };
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
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
