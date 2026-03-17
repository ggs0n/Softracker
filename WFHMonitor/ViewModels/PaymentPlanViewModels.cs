using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class PaymentPlansViewModel
{
    public SubscriptionPlan CurrentPlan { get; set; } = SubscriptionPlan.Free;
    public bool IsProSubscriptionActive { get; set; }
    public DateTime? ProSubscribedAt { get; set; }
    public bool IsStripeBillingConfigured { get; set; }

    public int CurrentProjectCount { get; set; }
    public int CurrentBugCount { get; set; }
    public int CurrentFeatureCount { get; set; }

    public int FreeProjectLimit { get; set; } = ProVersionDefaults.FreeProjectLimit;
    public int FreeBugLimit { get; set; } = ProVersionDefaults.FreeBugLimit;
    public int FreeFeatureLimit { get; set; } = ProVersionDefaults.FreeFeatureLimit;
    public bool AllowOpenClawForFreePlan { get; set; }

    public bool RequiresProPayment => CurrentPlan == SubscriptionPlan.Pro && !IsProSubscriptionActive;
    public bool CanStartProCheckout => RequiresProPayment && IsStripeBillingConfigured;
    public bool IsFreeProjectLimitReached => CurrentProjectCount >= FreeProjectLimit;
    public bool IsFreeBugLimitReached => CurrentBugCount >= FreeBugLimit;
    public bool IsFreeFeatureLimitReached => CurrentFeatureCount >= FreeFeatureLimit;
}
