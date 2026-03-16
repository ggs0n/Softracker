using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class PaymentPlansViewModel
{
    public SubscriptionPlan CurrentPlan { get; set; } = SubscriptionPlan.Free;
    public bool IsProSubscriptionActive { get; set; }
    public DateTime? ProSubscribedAt { get; set; }
    public bool IsStripeBillingConfigured { get; set; }

    public int CurrentCrCount { get; set; }
    public int CurrentBugCount { get; set; }

    public int FreeCrLimit { get; set; } = 5;
    public int FreeBugLimit { get; set; } = 5;

    public bool RequiresProPayment => CurrentPlan == SubscriptionPlan.Pro && !IsProSubscriptionActive;
    public bool CanStartProCheckout => RequiresProPayment && IsStripeBillingConfigured;
    public bool IsFreeCrLimitReached => CurrentCrCount >= FreeCrLimit;
    public bool IsFreeBugLimitReached => CurrentBugCount >= FreeBugLimit;
}
