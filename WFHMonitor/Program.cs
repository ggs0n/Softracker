using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services;
using WFHMonitor.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")), poolSize: 128);

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
    options.LoginPath = "/Auth/Login";
    options.LogoutPath = "/Auth/Logout";
    options.AccessDeniedPath = "/Auth/AccessDenied";
});

builder.Services.Configure<GitHubSettings>(
    builder.Configuration.GetSection("GitHubSettings"));
builder.Services.Configure<GitHubOAuthSettings>(
    builder.Configuration.GetSection("GitHubOAuth"));
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<ProjectMonitoringSettings>(
    builder.Configuration.GetSection("ProjectMonitoring"));
builder.Services.Configure<StripeBillingSettings>(
    builder.Configuration.GetSection("StripeBilling"));
builder.Services.Configure<OpenClawSettings>(
    builder.Configuration.GetSection("OpenClaw"));
builder.Services.AddHttpClient<IGitHubService, GitHubService>();
builder.Services.AddScoped<IGitHubOAuthService, GitHubOAuthService>();
builder.Services.AddHttpClient<IStripeBillingService, StripeBillingService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IUserRegistrationService, UserRegistrationService>();
builder.Services.AddScoped<IDeveloperSummaryService, DeveloperSummaryService>();
builder.Services.AddScoped<IBugService, BugService>();
builder.Services.AddScoped<IOpenClawBugScanService, OpenClawBugScanService>();
builder.Services.AddSingleton<BugFixQueueService>();
builder.Services.AddSingleton<IBugFixQueueService>(sp => sp.GetRequiredService<BugFixQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<BugFixQueueService>());
builder.Services.AddSingleton<FeatureAgentQueueService>();
builder.Services.AddSingleton<IFeatureAgentQueueService>(sp => sp.GetRequiredService<FeatureAgentQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<FeatureAgentQueueService>());
builder.Services.AddSingleton<ProjectBugScanQueueService>();
builder.Services.AddSingleton<IProjectBugScanQueueService>(sp => sp.GetRequiredService<ProjectBugScanQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ProjectBugScanQueueService>());
builder.Services.AddSingleton<QaOpenClawQueueService>();
builder.Services.AddSingleton<IQaOpenClawQueueService>(sp => sp.GetRequiredService<QaOpenClawQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<QaOpenClawQueueService>());
builder.Services.AddScoped<IUserRoleCacheService, UserRoleCacheService>();
builder.Services.AddScoped<ITaskBoardService, TaskBoardService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ISystemSettingsService, SystemSettingsService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<IOutlookCalendarSyncService, OutlookCalendarSyncService>();
builder.Services.AddScoped<IProjectMonitoringService, ProjectMonitoringService>();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(opts => opts.Level = CompressionLevel.SmallestSize);

builder.Services.AddRateLimiter(opts =>
{
    opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.User?.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            }));
    opts.RejectionStatusCode = 429;
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=604800")
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<WFHMonitor.Services.UserActivityMiddleware>();

app.MapHealthChecks("/health");
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
