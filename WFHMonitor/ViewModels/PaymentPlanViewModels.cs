using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class PaymentPlansViewModel
{
    public SubscriptionPlan CurrentPlan { get; set; } = SubscriptionPlan.Free;
    public bool IsProSubscriptionActive { get; set; }
    public DateTime? ProSubscribedAt { get; set; }
    public DateTime? ProSubscriptionEndsAt { get; set; }
    public bool IsProCancelAtPeriodEnd { get; set; }
    public bool IsStripeBillingConfigured { get; set; }
    public bool HasPendingStripeCheckout { get; set; }
    public string? PendingStripeCheckoutUrl { get; set; }

    public int CurrentProjectCount { get; set; }
    public int CurrentBugCount { get; set; }
    public int CurrentFeatureCount { get; set; }

    public int FreeProjectLimit { get; set; } = ProVersionDefaults.FreeProjectLimit;
    public int FreeBugLimit { get; set; } = ProVersionDefaults.FreeBugLimit;
    public int FreeFeatureLimit { get; set; } = ProVersionDefaults.FreeFeatureLimit;
    public bool AllowCodexForFreePlan { get; set; }

    public bool HasProAccess => IsProSubscriptionActive &&
                                (!ProSubscriptionEndsAt.HasValue || ProSubscriptionEndsAt.Value > DateTime.UtcNow);
    public bool RequiresProPayment => (CurrentPlan == SubscriptionPlan.Pro || HasPendingStripeCheckout) && !HasProAccess;
    public bool CanStartProCheckout => RequiresProPayment && IsStripeBillingConfigured;
    public bool CanCancelProPlan => HasProAccess && CurrentPlan == SubscriptionPlan.Pro;
    public bool IsFreeProjectLimitReached => CurrentProjectCount >= FreeProjectLimit;
    public bool IsFreeBugLimitReached => CurrentBugCount >= FreeBugLimit;
    public bool IsFreeFeatureLimitReached => CurrentFeatureCount >= FreeFeatureLimit;
}
