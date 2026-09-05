using System.Text.Json.Serialization;
using WFHMonitor.AutomationService;
using WFHMonitor.AutomationService.Configuration;
using WFHMonitor.AutomationService.Services;
using WFHMonitor.Services;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.Configure<CodexSettings>(
    builder.Configuration.GetSection(CodexSettings.SectionName));
builder.Services.Configure<AiAutomationSettings>(
    builder.Configuration.GetSection(AiAutomationSettings.SectionName));
builder.Services.AddSingleton<CodexAppServerClient>();
builder.Services.AddSingleton<ICodexAppServerClient>(
    services => services.GetRequiredService<CodexAppServerClient>());
builder.Services.AddHostedService(
    services => services.GetRequiredService<CodexAppServerClient>());
builder.Services.AddSingleton<PromptAssetLoader>();
builder.Services.AddScoped<CodexAiAutomationService>();
builder.Services.AddScoped<IAiAutomationService>(
    services => services.GetRequiredService<CodexAiAutomationService>());
builder.Services.AddScoped<IAiAccountService>(
    services => services.GetRequiredService<CodexAiAutomationService>());
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.ReferenceHandler =
        ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.Converters.Add(
        new JsonStringEnumConverter());
});

var app = builder.Build();

app.UseMiddleware<ServiceSharedSecretMiddleware>();
app.MapHealthChecks("/health");

app.MapGet(
    "/api/automation/account",
    async (
        IAiAccountService account,
        CancellationToken cancellationToken) =>
        Results.Ok(await account.GetStatusAsync(cancellationToken)));

app.MapPost(
    "/api/automation/account/login",
    async (
        IAiAccountService account,
        CancellationToken cancellationToken) =>
        Results.Ok(await account.StartLoginAsync(cancellationToken)));

app.MapGet(
    "/api/automation/account/login/{loginId}",
    async (
        string loginId,
        IAiAccountService account,
        CancellationToken cancellationToken) =>
        Results.Ok(await account.GetLoginStatusAsync(
            loginId,
            cancellationToken)));

app.MapPost(
    "/api/automation/account/login/{loginId}/cancel",
    async (
        string loginId,
        IAiAccountService account,
        CancellationToken cancellationToken) =>
    {
        await account.CancelLoginAsync(loginId, cancellationToken);
        return Results.NoContent();
    });

app.MapPost(
    "/api/automation/account/logout",
    async (
        IAiAccountService account,
        CancellationToken cancellationToken) =>
    {
        await account.LogoutAsync(cancellationToken);
        return Results.NoContent();
    });

app.MapPost(
    "/api/automation/brainstorm",
    async (
        BrainstormGenerateDesignRequest request,
        IAiAutomationService automation,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(
                await automation.GenerateBrainstormAsync(
                    request,
                    cancellationToken));
        }
        catch (Exception exception)
        {
            app.Logger.LogError(
                exception,
                "Brainstorm generation failed.");
            return Results.Problem(
                "Brainstorm generation failed.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    });

app.MapPost(
    "/api/automation/module-images",
    async (
        AiModuleImageGenerationRequest request,
        IAiAutomationService automation,
        CancellationToken cancellationToken) =>
    {
        try
        {
            return Results.Ok(
                await automation.GenerateModuleImagesAsync(
                    request,
                    cancellationToken));
        }
        catch (Exception exception)
        {
            app.Logger.LogError(
                exception,
                "Module image generation failed.");
            return Results.Problem(
                "Module image generation failed.",
                statusCode: StatusCodes.Status502BadGateway);
        }
    });

app.MapPost(
    "/api/automation/scan-project",
    async (
        AiScanProjectRequest request,
        IAiAutomationService automation,
        CancellationToken cancellationToken) =>
    {
        if (request.Project is null)
            return Results.BadRequest(
                new { error = "A project payload is required." });
        return Results.Ok(await automation.ScanProjectAsync(
            request.Project,
            request.WorkspacePath,
            cancellationToken,
            request.UseSecurityPrompt));
    });

app.MapPost(
    "/api/automation/scan-module",
    async (
        AiScanModuleRequest request,
        IAiAutomationService automation,
        CancellationToken cancellationToken) =>
    {
        if (request.Project is null
            || string.IsNullOrWhiteSpace(request.ModuleName))
        {
            return Results.BadRequest(
                new
                {
                    error = "A project and module name are required."
                });
        }

        return Results.Ok(await automation.ScanModuleAsync(
            request.Project,
            request.ModuleName,
            request.WorkspacePath,
            cancellationToken,
            request.UseSecurityPrompt));
    });

app.MapPost(
    "/api/automation/fix-bug",
    async (
        AiFixBugRequest request,
        IAiAutomationService automation,
        CancellationToken cancellationToken) =>
    {
        if (request.Bug is null)
            return Results.BadRequest(
                new { error = "A bug payload is required." });
        return Results.Ok(await automation.FixBugAsync(
            request.Bug,
            request.WorkspacePath,
            cancellationToken));
    });

app.MapPost(
    "/api/automation/implement-feature",
    async (
        AiFeatureGuidanceRequest request,
        IAiAutomationService automation,
        CancellationToken cancellationToken) =>
    {
        if (request.Feature is null)
            return Results.BadRequest(
                new { error = "A feature payload is required." });
        return Results.Ok(await automation.ImplementFeatureAsync(
            request.Feature,
            request.Project,
            request.WorkspacePath,
            cancellationToken));
    });

app.MapPost(
    "/api/automation/generate-tests",
    async (
        AiGenerateTestsRequest request,
        IAiAutomationService automation,
        CancellationToken cancellationToken) =>
    {
        if (request.Project is null)
            return Results.BadRequest(
                new { error = "A project payload is required." });
        return Results.Ok(await automation.GenerateTestCasesAsync(
            request.Project,
            request.WorkspacePath,
            cancellationToken));
    });

app.MapPost(
    "/api/automation/analyze-health",
    async (
        AiAnalyzeHealthRequest request,
        IAiAutomationService automation,
        CancellationToken cancellationToken) =>
    {
        if (request.Project is null)
            return Results.BadRequest(
                new { error = "A project payload is required." });
        return Results.Ok(await automation.AnalyzeProjectHealthAsync(
            request.Project,
            request.TotalBugs,
            request.OpenBugs,
            request.FeatureCount,
            request.RepositoryFeatureCount,
            request.TimelineDays,
            request.ComplexityScore,
            request.WorkspacePath,
            cancellationToken));
    });

app.Run();
