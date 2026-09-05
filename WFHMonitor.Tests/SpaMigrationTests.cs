using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WFHMonitor.Configuration;
using WFHMonitor.Infrastructure.Json;
using WFHMonitor.Infrastructure.Spa;
using WFHMonitor.Models;
using WFHMonitor.Services;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Tests;

public sealed class SpaMigrationTests
{
    [Fact]
    public void ApplicationUserJson_ExposesProfileButNeverIdentityOrProviderSecrets()
    {
        var user = new ApplicationUser
        {
            Id = "user-7",
            UserName = "engineer@example.com",
            Email = "engineer@example.com",
            FullName = "Amina Hassan",
            CompanyName = "Northstar Labs",
            PasswordHash = "secret-password-hash",
            SecurityStamp = "secret-security-stamp",
            ConcurrencyStamp = "secret-concurrency-stamp",
            StripeCustomerId = "cus_secret",
            OutlookCalendarIcsUrl = "https://calendar.example.com/private-feed"
        };
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new SafeApplicationUserJsonConverter());

        var json = JsonSerializer.Serialize(user, options);

        Assert.Contains("\"fullName\":\"Amina Hassan\"", json);
        Assert.Contains("\"companyName\":\"Northstar Labs\"", json);
        Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("concurrencyStamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cus_secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-feed", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ApiViewResult_BecomesTypedJsonEnvelopeWithValidationErrors()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/Projects/Create";
        var controller = new TestController
        {
            TempData = new TempDataDictionary(
                httpContext,
                Mock.Of<ITempDataProvider>())
        };
        controller.ModelState.AddModelError("Title", "Project title is required.");
        var viewResult = new ViewResult
        {
            ViewData = new ViewDataDictionary(
                new EmptyModelMetadataProvider(),
                controller.ModelState)
            {
                Model = new { Title = string.Empty }
            }
        };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());
        var filterContext = new ResultExecutingContext(
            actionContext,
            [],
            viewResult,
            controller);

        new SpaApiCompatibilityFilter().OnResultExecuting(filterContext);

        var jsonResult = Assert.IsType<JsonResult>(filterContext.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, jsonResult.StatusCode);
        var envelope = Assert.IsType<SpaApiEnvelope>(jsonResult.Value);
        Assert.Equal("Project title is required.", envelope.Errors["Title"][0]);
        Assert.NotNull(envelope.Data);
    }

    [Fact]
    public void LegacyMvcView_RedirectsToItsReactRoute()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/Bug/Details/17";
        var actionContext = new ActionContext(
            httpContext,
            new RouteData
            {
                Values =
                {
                    ["controller"] = "Bug",
                    ["action"] = "Details",
                    ["id"] = "17"
                }
            },
            new ActionDescriptor());
        var filterContext = new ResultExecutingContext(
            actionContext,
            [],
            new ViewResult(),
            new TestController());

        new SpaApiCompatibilityFilter().OnResultExecuting(filterContext);

        var redirect = Assert.IsType<RedirectResult>(filterContext.Result);
        Assert.Equal("/app/bugs/17", redirect.Url);
    }

    [Fact]
    public async Task AutomationClient_AuthenticatesAndStripsIdentitySecrets()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://automation.internal/")
        };
        var service = new AiAutomationRemoteService(
            httpClient,
            Options.Create(new AiAutomationServiceSettings
            {
                SharedSecret = "service-secret"
            }),
            NullLogger<AiAutomationRemoteService>.Instance);
        var project = new ChangeRequest
        {
            Id = 19,
            CrNumber = "CR-0019",
            Title = "Secure automation boundary",
            CreatedById = "user-19",
            CreatedBy = new ApplicationUser
            {
                Id = "user-19",
                FullName = "Amina Hassan",
                UserName = "amina@example.com",
                PasswordHash = "never-send-this",
                SecurityStamp = "never-send-this-either",
                StripeCustomerId = "cus_private"
            }
        };

        var result = await service.ScanProjectAsync(project);

        Assert.True(result.Succeeded);
        Assert.Equal("service-secret", handler.ServiceKey);
        Assert.Contains("Secure automation boundary", handler.RequestBody);
        Assert.Contains("Amina Hassan", handler.RequestBody);
        Assert.DoesNotContain("never-send-this", handler.RequestBody);
        Assert.DoesNotContain("cus_private", handler.RequestBody);
    }

    private sealed class TestController : Controller
    {
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;
        public string ServiceKey { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            ServiceKey = request.Headers.TryGetValues("X-Service-Secret", out var values)
                ? values.Single()
                : string.Empty;

            var responseBody = JsonSerializer.Serialize(
                new AiBugScanResult(true, string.Empty, [], string.Empty),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
