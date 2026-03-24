using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public class StripeBillingSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string ProPriceId { get; set; } = string.Empty;
    public long ProUnitAmount { get; set; } = 2900;
    public string Currency { get; set; } = "usd";
    public string ProductName { get; set; } = "Softracker Pro";
}

public class StripeBillingService : IStripeBillingService
{
    private readonly HttpClient _http;
    private readonly StripeBillingSettings _settings;

    public StripeBillingService(HttpClient http, IOptions<StripeBillingSettings> settings)
    {
        _http = http;
        _settings = settings.Value ?? new StripeBillingSettings();
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_settings.SecretKey) &&
        (!string.IsNullOrWhiteSpace(_settings.ProPriceId) || _settings.ProUnitAmount > 0);

    public async Task<(bool Succeeded, string CheckoutUrl, string SessionId, string Error)> CreateProCheckoutSessionAsync(
        ApplicationUser user,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return (false, string.Empty, string.Empty, "Stripe billing is not configured.");

        var payload = new List<KeyValuePair<string, string>>
        {
            new("mode", "subscription"),
            new("success_url", successUrl),
            new("cancel_url", cancelUrl),
            new("client_reference_id", user.Id),
            new("metadata[userId]", user.Id),
            new("line_items[0][quantity]", "1")
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
            payload.Add(new KeyValuePair<string, string>("customer_email", user.Email));

        if (!string.IsNullOrWhiteSpace(_settings.ProPriceId))
        {
            payload.Add(new KeyValuePair<string, string>("line_items[0][price]", _settings.ProPriceId.Trim()));
        }
        else
        {
            payload.Add(new KeyValuePair<string, string>("line_items[0][price_data][currency]", _settings.Currency.Trim().ToLowerInvariant()));
            payload.Add(new KeyValuePair<string, string>("line_items[0][price_data][unit_amount]", _settings.ProUnitAmount.ToString()));
            payload.Add(new KeyValuePair<string, string>("line_items[0][price_data][recurring][interval]", "month"));
            payload.Add(new KeyValuePair<string, string>("line_items[0][price_data][product_data][name]", _settings.ProductName.Trim()));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/checkout/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey.Trim());
        request.Content = new FormUrlEncodedContent(payload);

        using var response = await _http.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (false, string.Empty, string.Empty, ParseStripeError(content, response.ReasonPhrase));

        using var doc = JsonDocument.Parse(content);
        var url = doc.RootElement.TryGetProperty("url", out var urlElement)
            ? urlElement.GetString()
            : null;
        var sessionId = doc.RootElement.TryGetProperty("id", out var idElement)
            ? idElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(sessionId))
            return (false, string.Empty, string.Empty, "Stripe did not return a valid checkout session.");

        return (true, url, sessionId, string.Empty);
    }

    public async Task<(bool Succeeded, StripeCheckoutSessionInfo? Session, string Error)> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return (false, null, "Stripe billing is not configured.");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.stripe.com/v1/checkout/sessions/{Uri.EscapeDataString(sessionId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey.Trim());

        using var response = await _http.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (false, null, ParseStripeError(content, response.ReasonPhrase));

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        var session = new StripeCheckoutSessionInfo(
            root.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty,
            root.TryGetProperty("status", out var statusElement) ? statusElement.GetString() ?? string.Empty : string.Empty,
            root.TryGetProperty("payment_status", out var paymentStatusElement) ? paymentStatusElement.GetString() ?? string.Empty : string.Empty,
            root.TryGetProperty("customer", out var customerElement) ? customerElement.GetString() : null,
            root.TryGetProperty("subscription", out var subscriptionElement) ? subscriptionElement.GetString() : null,
            root.TryGetProperty("client_reference_id", out var clientRefElement) ? clientRefElement.GetString() : null
        );

        return (true, session, string.Empty);
    }

    public async Task<(bool Succeeded, string Error)> SetSubscriptionCancelAtPeriodEndAsync(
        string subscriptionId,
        bool cancelAtPeriodEnd,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            return (false, "Stripe billing is not configured.");

        if (string.IsNullOrWhiteSpace(subscriptionId))
            return (false, "Stripe subscription id is missing.");

        var payload = new List<KeyValuePair<string, string>>
        {
            new("cancel_at_period_end", cancelAtPeriodEnd ? "true" : "false")
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.stripe.com/v1/subscriptions/{Uri.EscapeDataString(subscriptionId)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SecretKey.Trim());
        request.Content = new FormUrlEncodedContent(payload);

        using var response = await _http.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (false, ParseStripeError(content, response.ReasonPhrase));

        return (true, string.Empty);
    }

    private static string ParseStripeError(string content, string? reasonPhrase)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? "Stripe request failed.";
            }
        }
        catch
        {
            // Ignore parse failures and use fallback message.
        }

        return string.IsNullOrWhiteSpace(reasonPhrase) ? "Stripe request failed." : reasonPhrase;
    }
}
