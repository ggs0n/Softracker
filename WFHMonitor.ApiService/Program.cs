using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using WFHMonitor.Configuration;
using WFHMonitor.Data;
using WFHMonitor.Infrastructure.Json;
using WFHMonitor.Infrastructure.Spa;
using WFHMonitor.Models;
using WFHMonitor.Services;
using WFHMonitor.Services.Interfaces;
using System.Text.Json.Serialization;

var bootstrapConfigurationBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
var bootstrapEnvironmentName =
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
if (!string.IsNullOrWhiteSpace(bootstrapEnvironmentName))
{
    bootstrapConfigurationBuilder.AddJsonFile(
        $"appsettings.{bootstrapEnvironmentName}.json",
        optional: true,
        reloadOnChange: false);
}

var bootstrapConfiguration = bootstrapConfigurationBuilder
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();
var bootstrapStorageSettings = GetRequiredSettings<FileStorageSettings>(
    bootstrapConfiguration,
    FileStorageSettings.SectionName);
var configuredWebRoot = ResolvePath(
    bootstrapStorageSettings.WebRootPath,
    Directory.GetCurrentDirectory(),
    "FileStorage:WebRootPath");

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = configuredWebRoot
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var databaseSettings = GetRequiredSettings<DatabaseRuntimeSettings>(
    builder.Configuration,
    DatabaseRuntimeSettings.SectionName);
var storageSettings = GetRequiredSettings<FileStorageSettings>(
    builder.Configuration,
    FileStorageSettings.SectionName);
var rateLimitingSettings = GetRequiredSettings<RateLimitingSettings>(
    builder.Configuration,
    RateLimitingSettings.SectionName);
var compressionSettings = GetRequiredSettings<ApiCompressionSettings>(
    builder.Configuration,
    ApiCompressionSettings.SectionName);
var aiAutomationServiceSettings =
    GetRequiredSettings<AiAutomationServiceSettings>(
        builder.Configuration,
        AiAutomationServiceSettings.SectionName);
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is required.");
if (databaseSettings.DbContextPoolSize <= 0)
    throw new InvalidOperationException(
        "Database:DbContextPoolSize must be greater than zero.");
if (string.IsNullOrWhiteSpace(storageSettings.UploadsDirectoryName))
    throw new InvalidOperationException(
        "FileStorage:UploadsDirectoryName is required.");
if (storageSettings.CacheMaxAgeSeconds < 0)
    throw new InvalidOperationException(
        "FileStorage:CacheMaxAgeSeconds cannot be negative.");
if (rateLimitingSettings.PermitLimit <= 0
    || rateLimitingSettings.WindowSeconds <= 0)
{
    throw new InvalidOperationException(
        "RateLimiting values must be greater than zero.");
}
if (!Uri.TryCreate(
        aiAutomationServiceSettings.BaseUrl,
        UriKind.Absolute,
        out var aiAutomationServiceUri)
    || aiAutomationServiceUri.Scheme is not ("http" or "https"))
{
    throw new InvalidOperationException(
        "AiAutomationService:BaseUrl must be an absolute HTTP or HTTPS URL.");
}
if (aiAutomationServiceSettings.RequestTimeoutSeconds <= 0)
    throw new InvalidOperationException(
        "AiAutomationService:RequestTimeoutSeconds must be greater than zero.");

var brotliLevel = GetCompressionLevel(
    compressionSettings.BrotliLevel,
    "ResponseCompression:BrotliLevel");
var gzipLevel = GetCompressionLevel(
    compressionSettings.GzipLevel,
    "ResponseCompression:GzipLevel");
var uploadCacheControl =
    $"public, max-age={storageSettings.CacheMaxAgeSeconds}";

if (builder.Environment.IsDevelopment()
    && string.IsNullOrWhiteSpace(
        builder.Configuration["JwtSettings:SecretKey"]))
{
    builder.Configuration["JwtSettings:SecretKey"] =
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}

builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString),
    poolSize: databaseSettings.DbContextPoolSize);

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

if (builder.Environment.IsDevelopment())
{
    builder.Services.RemoveAll<IPasswordValidator<ApplicationUser>>();
    builder.Services.AddScoped<IPasswordValidator<ApplicationUser>, AllowAllPasswordValidator>();
    builder.Services.RemoveAll<IUserValidator<ApplicationUser>>();
    builder.Services.AddScoped<IUserValidator<ApplicationUser>, AllowAllUserValidator>();
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/api/Auth/Login";
    options.LogoutPath = "/api/Auth/Logout";
    options.AccessDeniedPath = "/api/Auth/AccessDenied";
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.Configure<GitHubSettings>(
    builder.Configuration.GetSection("GitHubSettings"));
builder.Services.Configure<GitHubOAuthSettings>(
    builder.Configuration.GetSection("GitHubOAuth"));
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<ProjectMonitoringSettings>(
    builder.Configuration.GetSection("ProjectMonitoring"));
builder.Services.Configure<GitAutomationSettings>(
    builder.Configuration.GetSection(
        GitAutomationSettings.SectionName));
builder.Services.Configure<StripeBillingSettings>(
    builder.Configuration.GetSection("StripeBilling"));
builder.Services.Configure<AiAutomationSettings>(
    builder.Configuration.GetSection(
        AiAutomationSettings.SectionName));
builder.Services.Configure<AiAutomationServiceSettings>(
    builder.Configuration.GetSection(
        AiAutomationServiceSettings.SectionName));
builder.Services.AddHttpClient<IGitHubService, GitHubService>();
builder.Services.AddScoped<IGitHubOAuthService, GitHubOAuthService>();
builder.Services.AddHttpClient<IStripeBillingService, StripeBillingService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUserRegistrationService, UserRegistrationService>();
builder.Services.AddScoped<IDeveloperSummaryService, DeveloperSummaryService>();
builder.Services.AddScoped<IBugService, BugService>();
builder.Services.AddScoped<
    IRepositoryWorkspaceService,
    RepositoryWorkspaceService>();
builder.Services.AddHostedService<
    RepositoryWorkspaceCleanupService>();
builder.Services.AddScoped<
    IBrainstormDesignGenerator,
    BrainstormDesignGenerator>();
builder.Services.AddSingleton<
    IBrainstormResultImporter,
    BrainstormResultImporter>();
var aiAutomationTimeout = GetTimeout(
    aiAutomationServiceSettings.RequestTimeoutSeconds,
    "AiAutomationService:RequestTimeoutSeconds");
builder.Services.AddHttpClient<
    IAiAutomationService,
    AiAutomationRemoteService>(client =>
{
    client.BaseAddress = aiAutomationServiceUri;
    client.Timeout = aiAutomationTimeout;
});
builder.Services.AddHttpClient<
    IAiAccountService,
    AiAutomationRemoteService>(client =>
{
    client.BaseAddress = aiAutomationServiceUri;
    client.Timeout = aiAutomationTimeout;
});
builder.Services.AddSingleton<BugFixQueueService>();
builder.Services.AddSingleton<IBugFixQueueService>(sp => sp.GetRequiredService<BugFixQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<BugFixQueueService>());
builder.Services.AddSingleton<FeatureAgentQueueService>();
builder.Services.AddSingleton<IFeatureAgentQueueService>(sp => sp.GetRequiredService<FeatureAgentQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<FeatureAgentQueueService>());
builder.Services.AddSingleton<ProjectBugScanQueueService>();
builder.Services.AddSingleton<IProjectBugScanQueueService>(sp => sp.GetRequiredService<ProjectBugScanQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ProjectBugScanQueueService>());
builder.Services.AddSingleton<QaAiAutomationQueueService>();
builder.Services.AddSingleton<IQaAiAutomationQueueService>(
    services =>
        services.GetRequiredService<QaAiAutomationQueueService>());
builder.Services.AddHostedService(
    services =>
        services.GetRequiredService<QaAiAutomationQueueService>());
builder.Services.AddScoped<IUserRoleCacheService, UserRoleCacheService>();
builder.Services.AddScoped<ITaskBoardService, TaskBoardService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<IOutlookCalendarSyncService, OutlookCalendarSyncService>();
builder.Services.AddScoped<IProjectMonitoringService, ProjectMonitoringService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

builder.Services.AddMemoryCache();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                               | ForwardedHeaders.XForwardedHost
                               | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(
    opts => opts.Level = brotliLevel);
builder.Services.Configure<GzipCompressionProviderOptions>(
    opts => opts.Level = gzipLevel);

builder.Services.AddRateLimiter(opts =>
{
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.User?.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingSettings.PermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitingSettings.WindowSeconds)
            }));
    opts.RejectionStatusCode = 429;
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add<SpaApiCompatibilityFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.Converters.Add(new SafeApplicationUserJsonConverter());
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                title = "The request could not be completed.",
                status = StatusCodes.Status500InternalServerError
            });
        });
    });
    app.UseHsts();
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseResponseCompression();
var uploadsPath = Path.Combine(
    app.Environment.WebRootPath,
    storageSettings.UploadsDirectoryName);
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    OnPrepareResponse = context =>
        context.Context.Response.Headers.CacheControl =
            uploadCacheControl
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<WFHMonitor.Services.UserActivityMiddleware>();

app.MapHealthChecks("/health");
app.MapControllerRoute(
    name: "spa-api",
    pattern: "api/{controller}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
    await DbInitializer.SeedAsync(scope.ServiceProvider, app.Environment.IsDevelopment());
}

app.Run();

static T GetRequiredSettings<T>(IConfiguration configuration, string sectionName)
    where T : class
{
    return configuration.GetRequiredSection(sectionName).Get<T>()
           ?? throw new InvalidOperationException(
               $"Configuration section '{sectionName}' is invalid.");
}

static string ResolvePath(
    string configuredPath,
    string basePath,
    string configurationKey)
{
    if (string.IsNullOrWhiteSpace(configuredPath))
        throw new InvalidOperationException($"{configurationKey} is required.");

    return Path.GetFullPath(
        Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(basePath, configuredPath));
}

static TimeSpan GetTimeout(int timeoutSeconds, string configurationKey)
{
    if (timeoutSeconds < 0)
        throw new InvalidOperationException(
            $"{configurationKey} cannot be negative.");

    return timeoutSeconds == 0
        ? Timeout.InfiniteTimeSpan
        : TimeSpan.FromSeconds(timeoutSeconds);
}

static CompressionLevel GetCompressionLevel(string value, string configurationKey)
{
    if (Enum.TryParse<CompressionLevel>(value, true, out var level))
        return level;

    throw new InvalidOperationException(
        $"{configurationKey} is not a valid compression level.");
}

public partial class Program;
