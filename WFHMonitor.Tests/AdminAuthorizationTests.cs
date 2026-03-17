using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using WFHMonitor.Controllers;

namespace WFHMonitor.Tests;

public class AdminAuthorizationTests
{
    [Fact]
    public void AdminController_IsRestrictedToAdminRole()
    {
        var authorize = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Admin", authorize!.Roles);
    }

    [Theory]
    [InlineData(nameof(AdminController.Index))]
    [InlineData(nameof(AdminController.Employees))]
    [InlineData(nameof(AdminController.AddEmployee))]
    [InlineData(nameof(AdminController.DeleteEmployee))]
    public void AdminActions_AreCoveredByControllerLevelAdminAuthorization(string actionName)
    {
        var controllerAuthorize = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();
        var action = typeof(AdminController).GetMethod(actionName);

        Assert.NotNull(action);
        Assert.NotNull(controllerAuthorize);
        Assert.Equal("Admin", controllerAuthorize!.Roles);

        var allowAnonymous = action!.GetCustomAttribute<AllowAnonymousAttribute>();
        Assert.Null(allowAnonymous);
    }
}
