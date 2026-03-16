using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public class ProjectMonitoringSettings
{
    public GoogleAnalyticsMonitoringSettings GoogleAnalytics { get; set; } = new();
    public StripeMonitoringSettings Stripe { get; set; } = new();
    public List<ProjectMonitoringTargetSettings> Projects { get; set; } = [];
}

public class GoogleAnalyticsMonitoringSettings
{
    public string AccessToken { get; set; } = string.Empty;
    public int LookbackDays { get; set; } = 30;
}

public class StripeMonitoringSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public int MaxPayments { get; set; } = 20;
}

public class ProjectMonitoringTargetSettings
{
    public int? ProjectId { get; set; }
    public string CrNumber { get; set; } = string.Empty;
    public string HealthCheckUrl { get; set; } = string.Empty;
    public string AwsRegion { get; set; } = string.Empty;
    public string GoogleAnalyticsPropertyId { get; set; } = string.Empty;
    public string GoogleAnalyticsPagePath { get; set; } = string.Empty;
}

public class ProjectMonitoringService : IProjectMonitoringService
{
    private const int RequestTimeoutSeconds = 10;

    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProjectMonitoringSettings _settings;

    public ProjectMonitoringService(
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<ProjectMonitoringSettings> settings)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value ?? new ProjectMonitoringSettings();
    }

    public async Task<MonitorDashboardViewModel> BuildDashboardAsync(CancellationToken cancellationToken = default)
    {
        var deployedProjects = await _db.ChangeRequests
            .AsNoTracking()
            .Where(c => c.Stage == CrStage.DeploymentComplete || c.Status == CrStatus.Done)
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .ToListAsync(cancellationToken);

        var stripeResult = await GetStripePaymentHistoryAsync(_settings.Stripe, cancellationToken);

        var monitorCards = await Task.WhenAll(
            deployedProjects.Select(p => BuildProjectCardAsync(p, cancellationToken)));

        return new MonitorDashboardViewModel
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Projects = monitorCards.ToList(),
            StripePayments = stripeResult.Payments,
            StripeMessage = stripeResult.Message
        };
    }

    private async Task<ProjectMonitorCardViewModel> BuildProjectCardAsync(
        ChangeRequest project,
        CancellationToken cancellationToken)
    {
        var projectSettings = ResolveProjectSettings(project, _settings.Projects);
        var awsHealthTask = CheckAwsHealthAsync(projectSettings?.HealthCheckUrl, cancellationToken);
        var analyticsTask = GetAnalyticsViewsAsync(projectSettings, _settings.GoogleAnalytics, cancellationToken);

        await Task.WhenAll(awsHealthTask, analyticsTask);

        var awsHealth = await awsHealthTask;
        var analytics = await analyticsTask;

        return new ProjectMonitorCardViewModel
        {
            ProjectId = project.Id,
            CrNumber = project.CrNumber,
            Title = project.Title,
            Stage = project.Stage.ToString(),
            Status = project.Status.ToString(),
            AwsRegion = string.IsNullOrWhiteSpace(projectSettings?.AwsRegion)
                ? "Not configured"
                : projectSettings.AwsRegion.Trim(),
            AwsHealth = awsHealth,
            AnalyticsViewsLast30Days = analytics.Views,
            AnalyticsLabel = analytics.Label,
            AnalyticsMessage = analytics.Message
        };
    }

    private async Task<MonitorStatusViewModel> CheckAwsHealthAsync(
        string? healthCheckUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(healthCheckUrl))
        {
            return new MonitorStatusViewModel
            {
                State = MonitorSignalState.Unknown,
                Label = "Unknown",
                Details = "Health check URL is not configured."
            };
        }

        if (!Uri.TryCreate(healthCheckUrl, UriKind.Absolute, out var uri))
        {
            return new MonitorStatusViewModel
            {
                State = MonitorSignalState.Unknown,
                Label = "Unknown",
                Details = "Health check URL is invalid."
            };
        }

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(RequestTimeoutSeconds));

            using var response = await client.SendAsync(request, timeout.Token);
            var payload = await response.Content.ReadAsStringAsync(timeout.Token);

            if (response.IsSuccessStatusCode)
            {
                var payloadStatus = ExtractStatusToken(payload);
                if (payloadStatus is "unhealthy" or "down" or "error" or "failed")
                {
                    return new MonitorStatusViewModel
                    {
                        State = MonitorSignalState.Unhealthy,
                        Label = "Down",
                        Details = $"Endpoint returned {payloadStatus}."
                    };
                }

                return new MonitorStatusViewModel
                {
                    State = MonitorSignalState.Healthy,
                    Label = "Healthy",
                    Details = $"{(int)response.StatusCode} {response.ReasonPhrase}"
                };
            }

            return new MonitorStatusViewModel
            {
                State = MonitorSignalState.Unhealthy,
                Label = "Down",
                Details = $"{(int)response.StatusCode} {response.ReasonPhrase}"
            };
        }
        catch (TaskCanceledException)
        {
            return new MonitorStatusViewModel
            {
                State = MonitorSignalState.Unhealthy,
                Label = "Down",
                Details = "Health check timed out."
            };
        }
        catch (Exception ex)
        {
            return new MonitorStatusViewModel
            {
                State = MonitorSignalState.Unhealthy,
                Label = "Down",
                Details = Truncate(ex.Message, 120)
            };
        }
    }

    private async Task<AnalyticsResult> GetAnalyticsViewsAsync(
        ProjectMonitoringTargetSettings? projectSettings,
        GoogleAnalyticsMonitoringSettings analyticsSettings,
        CancellationToken cancellationToken)
    {
        var lookbackDays = Math.Clamp(analyticsSettings.LookbackDays <= 0 ? 30 : analyticsSettings.LookbackDays, 1, 365);
        var label = $"Last {lookbackDays} days";

        if (projectSettings == null)
        {
            return new AnalyticsResult(null, "Project monitoring settings are not configured.", label);
        }

        var propertyId = NormalizePropertyId(projectSettings.GoogleAnalyticsPropertyId);
        if (string.IsNullOrWhiteSpace(propertyId))
        {
            return new AnalyticsResult(null, "Google Analytics property ID is not configured.", label);
        }

        if (string.IsNullOrWhiteSpace(analyticsSettings.AccessToken))
        {
            return new AnalyticsResult(null, "Google Analytics access token is not configured.", label);
        }

        var payload = new Dictionary<string, object?>
        {
            ["dateRanges"] = new[]
            {
                new
                {
                    startDate = $"{lookbackDays}daysAgo",
                    endDate = "today"
                }
            },
            ["metrics"] = new[]
            {
                new
                {
                    name = "screenPageViews"
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(projectSettings.GoogleAnalyticsPagePath))
        {
            payload["dimensionFilter"] = new
            {
                filter = new
                {
                    fieldName = "pagePath",
                    stringFilter = new
                    {
                        matchType = "EXACT",
                        value = projectSettings.GoogleAnalyticsPagePath.Trim()
                    }
                }
            };
        }

        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://analyticsdata.googleapis.com/v1beta/properties/{propertyId}:runReport");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", analyticsSettings.AccessToken.Trim());
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(RequestTimeoutSeconds));

            using var response = await client.SendAsync(request, timeout.Token);
            var content = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return new AnalyticsResult(
                    null,
                    $"Analytics API error: {(int)response.StatusCode} {response.ReasonPhrase}",
                    label);
            }

            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array ||
                rows.GetArrayLength() == 0)
            {
                return new AnalyticsResult(0, "No page views returned.", label);
            }

            var firstRow = rows[0];
            if (!firstRow.TryGetProperty("metricValues", out var metricValues) ||
                metricValues.ValueKind != JsonValueKind.Array ||
                metricValues.GetArrayLength() == 0)
            {
                return new AnalyticsResult(0, "No page views returned.", label);
            }

            var firstMetric = metricValues[0];
            var rawValue = firstMetric.TryGetProperty("value", out var valueElement)
                ? valueElement.GetString()
                : null;

            if (!long.TryParse(rawValue, out var views))
                views = 0;

            return new AnalyticsResult(views, string.Empty, label);
        }
        catch (TaskCanceledException)
        {
            return new AnalyticsResult(null, "Analytics request timed out.", label);
        }
        catch (Exception ex)
        {
            return new AnalyticsResult(null, Truncate(ex.Message, 120), label);
        }
    }

    private async Task<StripeResult> GetStripePaymentHistoryAsync(
        StripeMonitoringSettings stripeSettings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stripeSettings.SecretKey))
            return new StripeResult([], "Stripe secret key is not configured.");

        var limit = Math.Clamp(stripeSettings.MaxPayments <= 0 ? 20 : stripeSettings.MaxPayments, 1, 100);
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.stripe.com/v1/payment_intents?limit={limit}");

        var basicToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{stripeSettings.SecretKey.Trim()}:"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicToken);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(RequestTimeoutSeconds));

            using var response = await client.SendAsync(request, timeout.Token);
            var content = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                return new StripeResult(
                    [],
                    $"Stripe API error: {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return new StripeResult([], "Stripe returned no payment history.");
            }

            var payments = new List<StripePaymentHistoryViewModel>();

            foreach (var item in data.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
                var amount = item.TryGetProperty("amount", out var amountElement) && amountElement.TryGetInt64(out var cents)
                    ? cents / 100m
                    : 0m;
                var currency = item.TryGetProperty("currency", out var currencyElement)
                    ? (currencyElement.GetString() ?? "usd").ToUpperInvariant()
                    : "USD";
                var status = item.TryGetProperty("status", out var statusElement)
                    ? statusElement.GetString() ?? "unknown"
                    : "unknown";
                var description = item.TryGetProperty("description", out var descriptionElement)
                    ? descriptionElement.GetString() ?? string.Empty
                    : string.Empty;
                var createdAt = item.TryGetProperty("created", out var createdElement) && createdElement.TryGetInt64(out var unixSeconds)
                    ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime
                    : DateTime.MinValue;

                payments.Add(new StripePaymentHistoryViewModel
                {
                    Id = id,
                    CreatedAtUtc = createdAt,
                    Amount = amount,
                    Currency = currency,
                    Status = status,
                    Description = string.IsNullOrWhiteSpace(description) ? "-" : description
                });
            }

            return new StripeResult(payments, string.Empty);
        }
        catch (TaskCanceledException)
        {
            return new StripeResult([], "Stripe request timed out.");
        }
        catch (Exception ex)
        {
            return new StripeResult([], Truncate(ex.Message, 120));
        }
    }

    private static ProjectMonitoringTargetSettings? ResolveProjectSettings(
        ChangeRequest project,
        IReadOnlyCollection<ProjectMonitoringTargetSettings>? configuredProjects)
    {
        if (configuredProjects == null || configuredProjects.Count == 0)
            return null;

        var byId = configuredProjects.FirstOrDefault(c => c.ProjectId.HasValue && c.ProjectId.Value == project.Id);
        if (byId != null)
            return byId;

        return configuredProjects.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(c.CrNumber) &&
            string.Equals(c.CrNumber.Trim(), project.CrNumber, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePropertyId(string? propertyId)
    {
        if (string.IsNullOrWhiteSpace(propertyId))
            return string.Empty;

        var value = propertyId.Trim();
        const string prefix = "properties/";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return value[prefix.Length..];

        return value;
    }

    private static string ExtractStatusToken(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("status", out var statusElement))
                return statusElement.GetString()?.Trim().ToLowerInvariant() ?? string.Empty;
        }
        catch
        {
            // Ignore payload parsing failures; many health endpoints return plain text.
        }

        var normalized = payload.Trim().ToLowerInvariant();
        if (normalized.Contains("unhealthy", StringComparison.Ordinal))
            return "unhealthy";
        if (normalized.Contains("healthy", StringComparison.Ordinal))
            return "healthy";
        if (normalized.Contains("down", StringComparison.Ordinal))
            return "down";
        if (normalized.Contains("error", StringComparison.Ordinal))
            return "error";

        return string.Empty;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= maxLength ? value : $"{value[..maxLength].Trim()}...";
    }

    private sealed record AnalyticsResult(long? Views, string Message, string Label);
    private sealed record StripeResult(List<StripePaymentHistoryViewModel> Payments, string Message);
}
