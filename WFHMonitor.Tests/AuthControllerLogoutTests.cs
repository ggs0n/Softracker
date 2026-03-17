using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WFHMonitor.Controllers;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Tests;

public class AuthControllerLogoutTests
{
    [Fact]
    public async Task Logout_ClearsAuthArtifactsAndRedirectsToLogin()
    {
        var signInManager = CreateSignInManagerMock();
        var controller = new AuthController(
            CreateUserManagerMock().Object,
            signInManager.Object,
            CreateRoleManagerMock().Object,
            Mock.Of<IJwtTokenService>(),
            Mock.Of<IUserRegistrationService>());

        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService, TestAuthenticationService>();
        httpContext.RequestServices = services.BuildServiceProvider();

        var session = new TestSession();
        httpContext.Features.Set<ISessionFeature>(new TestSessionFeature { Session = session });

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.Url = Mock.Of<IUrlHelper>();

        var result = await controller.Logout();

        signInManager.Verify(x => x.SignOutAsync(), Times.Once);
        Assert.True(session.WasCleared);

        var setCookieHeaders = httpContext.Response.Headers["Set-Cookie"].ToArray();
        Assert.Contains(setCookieHeaders, header => header.Contains("jwt_token=", StringComparison.Ordinal));
        Assert.Contains(setCookieHeaders, header => header.Contains("new_user_jwt_token=", StringComparison.Ordinal));
        Assert.Contains(setCookieHeaders, header => header.Contains(IdentityConstants.ApplicationScheme + "=", StringComparison.Ordinal));
        Assert.Equal("no-store, no-cache, max-age=0, must-revalidate", httpContext.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-cache", httpContext.Response.Headers.Pragma.ToString());
        Assert.Equal("\"cookies\", \"storage\", \"cache\"", httpContext.Response.Headers["Clear-Site-Data"].ToString());

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

    private sealed class TestAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, System.Security.Claims.ClaimsPrincipal principal, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }

    private sealed class TestSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new TestSession();
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public bool WasCleared { get; private set; }
        public bool IsAvailable => true;
        public string Id => Guid.NewGuid().ToString("N");
        public IEnumerable<string> Keys => _store.Keys;
        public void Clear() { WasCleared = true; _store.Clear(); }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
