using System.Security.Cryptography;
using System.Text;

namespace WFHMonitor.AutomationService;

public sealed class ServiceSharedSecretMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServiceSharedSecretMiddleware> _logger;

    public ServiceSharedSecretMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<ServiceSharedSecretMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var configuredKey =
            _configuration["ServiceAuthentication:SharedSecret"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            _logger.LogCritical(
                "The automation service shared secret is not configured.");
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "The automation service is not configured."
            });
            return;
        }

        var suppliedKey =
            context.Request.Headers["X-Service-Secret"].ToString();
        var configuredBytes = Encoding.UTF8.GetBytes(configuredKey);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedKey);
        var isValid = configuredBytes.Length == suppliedBytes.Length
                      && CryptographicOperations.FixedTimeEquals(configuredBytes, suppliedBytes);

        if (!isValid)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Service authentication failed."
            });
            return;
        }

        await _next(context);
    }
}
