using WFHMonitor.Models;

namespace WFHMonitor.Services.Interfaces;

public interface IStripeBillingService
{
    bool IsConfigured { get; }

    Task<(bool Succeeded, string CheckoutUrl, string Error)> CreateProCheckoutSessionAsync(
        ApplicationUser user,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, StripeCheckoutSessionInfo? Session, string Error)> GetCheckoutSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}

public sealed record StripeCheckoutSessionInfo(
    string Id,
    string Status,
    string PaymentStatus,
    string? CustomerId,
    string? SubscriptionId,
    string? ClientReferenceId
);
