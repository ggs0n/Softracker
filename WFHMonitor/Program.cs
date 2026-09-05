using System.IO.Compression;
using System.Net;
using Microsoft.AspNetCore.ResponseCompression;
using WFHMonitor.Configuration;

var builder = WebApplication.CreateBuilder(args);

var proxySettings = GetRequiredSettings<ApiServiceProxySettings>(
    builder.Configuration,
    ApiServiceProxySettings.SectionName);
var assetCachingSettings = GetRequiredSettings<AssetCachingSettings>(
    builder.Configuration,
    AssetCachingSettings.SectionName);
var compressionSettings = GetRequiredSettings<CompressionSettings>(
    builder.Configuration,
    CompressionSettings.SectionName);

if (!Uri.TryCreate(proxySettings.BaseUrl, UriKind.Absolute, out var apiServiceUri))
    throw new InvalidOperationException("ApiService:BaseUrl must be an absolute URL.");
if (proxySettings.BackendPrefixes.Length == 0)
    throw new InvalidOperationException("ApiService:BackendPrefixes must contain at least one route.");
if (assetCachingSettings.MaxAgeSeconds < 0)
    throw new InvalidOperationException("AssetCaching:MaxAgeSeconds cannot be negative.");

var proxyTimeout = GetTimeout(
    proxySettings.RequestTimeoutSeconds,
    "ApiService:RequestTimeoutSeconds");
var brotliLevel = GetCompressionLevel(
    compressionSettings.BrotliLevel,
    "ResponseCompression:BrotliLevel");
var gzipLevel = GetCompressionLevel(
    compressionSettings.GzipLevel,
    "ResponseCompression:GzipLevel");
var assetCacheControl = $"public, max-age={assetCachingSettings.MaxAgeSeconds}";

builder.Services.AddHttpClient("ApiServiceProxy", client =>
{
    client.BaseAddress = apiServiceUri;
    client.Timeout = proxyTimeout;
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AllowAutoRedirect = false,
    AutomaticDecompression = DecompressionMethods.None,
    UseCookies = false
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(
    options => options.Level = brotliLevel);
builder.Services.Configure<GzipCompressionProviderOptions>(
    options => options.Level = gzipLevel);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        var response = context.Context.Response;
        if (context.Context.Request.Path.Value?.EndsWith(
                "/app/index.html",
                StringComparison.OrdinalIgnoreCase) == true)
            response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        else
            response.Headers.CacheControl = assetCacheControl;
    }
});

var proxyMethods = new[]
{
    HttpMethods.Get,
    HttpMethods.Head,
    HttpMethods.Post,
    HttpMethods.Put,
    HttpMethods.Patch,
    HttpMethods.Delete,
    HttpMethods.Options
};

app.MapMethods("/api/{**path}", proxyMethods, ProxyRequestAsync);
app.MapMethods("/uploads/{**path}", proxyMethods, ProxyRequestAsync);

foreach (var prefix in proxySettings.BackendPrefixes.Distinct(
             StringComparer.OrdinalIgnoreCase))
{
    if (!prefix.StartsWith('/'))
        throw new InvalidOperationException(
            $"ApiService:BackendPrefixes entry '{prefix}' must start with '/'.");

    app.MapMethods(prefix, proxyMethods, ProxyRequestAsync);
    app.MapMethods($"{prefix}/{{**path}}", proxyMethods, ProxyRequestAsync);
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    apiService = apiServiceUri.ToString()
}));
app.MapGet("/", () => Results.Redirect("/app/"));
app.MapFallbackToFile(
    "app/{*path:nonfile}",
    "app/index.html",
    new StaticFileOptions
    {
        OnPrepareResponse = context =>
            context.Context.Response.Headers.CacheControl =
                "no-cache, no-store, must-revalidate"
    });

app.Run();

static async Task ProxyRequestAsync(
    HttpContext context,
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger)
{
    var client = httpClientFactory.CreateClient("ApiServiceProxy");
    var target = context.Request.PathBase
                 + context.Request.Path
                 + context.Request.QueryString;
    using var request = new HttpRequestMessage(
        new HttpMethod(context.Request.Method),
        target);

    var hasBody = context.Request.ContentLength > 0
                  || context.Request.Headers.ContainsKey("Transfer-Encoding");
    if (hasBody)
        request.Content = new StreamContent(context.Request.Body);

    foreach (var header in context.Request.Headers)
    {
        if (IsHopByHopHeader(header.Key)
            || header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)
            || header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            continue;

        if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            request.Content?.Headers.TryAddWithoutValidation(
                header.Key,
                header.Value.ToArray());
    }

    request.Headers.TryAddWithoutValidation(
        "X-Forwarded-For",
        context.Connection.RemoteIpAddress?.ToString());
    request.Headers.TryAddWithoutValidation(
        "X-Forwarded-Host",
        context.Request.Host.Value);
    request.Headers.TryAddWithoutValidation(
        "X-Forwarded-Proto",
        context.Request.Scheme);

    try
    {
        using var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);

        context.Response.StatusCode = (int)response.StatusCode;
        CopyResponseHeaders(response.Headers, context.Response);
        CopyResponseHeaders(response.Content.Headers, context.Response);
        context.Response.Headers.Remove("transfer-encoding");

        if (!HttpMethods.IsHead(context.Request.Method))
            await response.Content.CopyToAsync(
                context.Response.Body,
                context.RequestAborted);
    }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
    {
    }
    catch (HttpRequestException exception)
    {
        logger.LogError(exception, "API service request failed for {Path}.", target);
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        await context.Response.WriteAsJsonAsync(
            new
            {
                title = "The API service is unavailable.",
                status = StatusCodes.Status502BadGateway
            },
            context.RequestAborted);
    }
}

static void CopyResponseHeaders(
    System.Net.Http.Headers.HttpHeaders source,
    HttpResponse response)
{
    foreach (var header in source)
    {
        if (!IsHopByHopHeader(header.Key))
            response.Headers[header.Key] = header.Value.ToArray();
    }
}

static bool IsHopByHopHeader(string name)
{
    return name.Equals("Connection", StringComparison.OrdinalIgnoreCase)
           || name.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase)
           || name.Equals("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase)
           || name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
           || name.Equals("TE", StringComparison.OrdinalIgnoreCase)
           || name.Equals("Trailer", StringComparison.OrdinalIgnoreCase)
           || name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
           || name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase);
}

static T GetRequiredSettings<T>(IConfiguration configuration, string sectionName)
    where T : class
{
    return configuration.GetRequiredSection(sectionName).Get<T>()
           ?? throw new InvalidOperationException(
               $"Configuration section '{sectionName}' is invalid.");
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
