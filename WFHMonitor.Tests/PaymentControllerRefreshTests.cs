using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using WFHMonitor.Controllers;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Tests;

public class PaymentControllerRefreshTests
{
    [Fact]
    public async Task StartProCheckout_ReusesExistingPendingCheckout_OnRetry()
    {
        var user = CreateUser();
        user.SubscriptionPlan = SubscriptionPlan.Pro;
        user.PendingStripeCheckoutSessionId = "sess_pending";
        user.PendingStripeCheckoutUrl = "https://checkout.stripe.com/c/pay/cs_test_pending";

        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).ReturnsAsync(user);

        var stripe = new Mock<IStripeBillingService>();
        stripe.SetupGet(x => x.IsConfigured).Returns(true);

        var controller = CreateController(userManager.Object, stripe.Object, CreateDbContext());
        AttachHttpContext(controller);

        var result = await controller.StartProCheckout(null);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal(user.PendingStripeCheckoutUrl, redirect.Url);
        stripe.Verify(x => x.CreateProCheckoutSessionAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Index_OpenPendingCheckout_ShowsPendingResumeState()
    {
        var user = CreateUser();
        user.SubscriptionPlan = SubscriptionPlan.Pro;
        user.PendingStripeCheckoutSessionId = "sess_pending";
        user.PendingStripeCheckoutUrl = "https://checkout.stripe.com/c/pay/cs_test_pending";

        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).Returns(user.Id);
        userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);

        var stripe = new Mock<IStripeBillingService>();
        stripe.SetupGet(x => x.IsConfigured).Returns(true);
        stripe.Setup(x => x.GetCheckoutSessionAsync("sess_pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new StripeCheckoutSessionInfo("sess_pending", "open", "unpaid", null, null, user.Id), string.Empty));

        var controller = CreateController(userManager.Object, stripe.Object, CreateDbContext());
        AttachHttpContext(controller);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<WFHMonitor.ViewModels.PaymentPlansViewModel>(view.Model);
        Assert.True(vm.HasPendingStripeCheckout);
        Assert.Equal(user.PendingStripeCheckoutUrl, vm.PendingStripeCheckoutUrl);
        Assert.True(vm.RequiresProPayment);
    }

    [Fact]
    public async Task Index_CompletedPendingCheckout_AutoActivatesProAndClearsPendingState()
    {
        var user = CreateUser();
        user.SubscriptionPlan = SubscriptionPlan.Pro;
        user.PendingStripeCheckoutSessionId = "sess_paid";
        user.PendingStripeCheckoutUrl = "https://checkout.stripe.com/c/pay/cs_test_paid";

        var userManager = CreateUserManagerMock();
        userManager.Setup(x => x.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).Returns(user.Id);
        userManager.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);
        userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var stripe = new Mock<IStripeBillingService>();
        stripe.SetupGet(x => x.IsConfigured).Returns(true);
        stripe.Setup(x => x.GetCheckoutSessionAsync("sess_paid", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, new StripeCheckoutSessionInfo("sess_paid", "complete", "paid", "cus_1", "sub_1", user.Id), string.Empty));

        var controller = CreateController(userManager.Object, stripe.Object, CreateDbContext());
        AttachHttpContext(controller);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<WFHMonitor.ViewModels.PaymentPlansViewModel>(view.Model);
        Assert.True(user.IsProSubscriptionActive);
        Assert.Null(user.PendingStripeCheckoutSessionId);
        Assert.False(vm.HasPendingStripeCheckout);
        Assert.True(vm.HasProAccess);
        userManager.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    private static PaymentController CreateController(UserManager<ApplicationUser> userManager, IStripeBillingService stripe, ApplicationDbContext db)
    {
        var settings = new Mock<ISystemSettingsService>();
        settings.Setup(x => x.GetProVersionSettingsAsync())
            .ReturnsAsync(new WFHMonitor.ViewModels.ProVersionSettingsViewModel());

        return new PaymentController(userManager, db, stripe, settings.Object);
    }

    private static void AttachHttpContext(Controller controller)
    {
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        var url = new Mock<IUrlHelper>();
        url.Setup(x => x.Action(It.IsAny<UrlActionContext>())).Returns("/Payment/Success");
        controller.Url = url.Object;
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
