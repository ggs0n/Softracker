using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize]
public class PaymentController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly IStripeBillingService _stripeBillingService;
    private readonly ISystemSettingsService _systemSettingsService;

    public PaymentController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IStripeBillingService stripeBillingService,
        ISystemSettingsService systemSettingsService)
    {
        _userManager = userManager;
        _db = db;
        _stripeBillingService = stripeBillingService;
        _systemSettingsService = systemSettingsService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Forbid();

        await EnsureSubscriptionWindowAsync(user);
        await ReconcilePendingCheckoutAsync(user);

        var proSettings = await _systemSettingsService.GetProVersionSettingsAsync();

        var vm = new PaymentPlansViewModel
        {
            CurrentPlan = user.SubscriptionPlan,
            IsProSubscriptionActive = user.IsProSubscriptionActive,
            ProSubscribedAt = user.ProSubscribedAt,
            ProSubscriptionEndsAt = ResolveProEndDate(user),
            IsProCancelAtPeriodEnd = user.IsProCancelAtPeriodEnd,
            IsStripeBillingConfigured = _stripeBillingService.IsConfigured,
            HasPendingStripeCheckout = !string.IsNullOrWhiteSpace(user.PendingStripeCheckoutSessionId),
            PendingStripeCheckoutUrl = user.PendingStripeCheckoutUrl,
            CurrentProjectCount = await _db.ChangeRequests.CountAsync(c => c.CreatedById == userId),
            CurrentBugCount = await _db.BugReports.CountAsync(b => b.CreatedById == userId),
            CurrentFeatureCount = await _db.ChangeRequests
                .Where(c => c.CreatedById == userId)
                .SelectMany(c => c.Features)
                .CountAsync(),
            FreeProjectLimit = proSettings.FreeProjectLimit,
            FreeBugLimit = proSettings.FreeBugLimit,
            FreeFeatureLimit = proSettings.FreeFeatureLimit,
            AllowCodexForFreePlan = proSettings.AllowCodexForFreePlan
        };

        ViewData["Title"] = "Subscription";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChoosePlan(SubscriptionPlan plan, string? returnUrl)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Forbid();

        await EnsureSubscriptionWindowAsync(user);

        if (plan == SubscriptionPlan.Free)
        {
            if (HasActiveProAccess(user))
            {
                if (user.IsProCancelAtPeriodEnd)
                {
                    TempData["Info"] = "Your Pro plan is already set to cancel at period end.";
                    return RedirectToAction(nameof(Index));
                }

                var stripeSubId = user.StripeSubscriptionId?.Trim();
                if (!string.IsNullOrWhiteSpace(stripeSubId))
                {
                    var stripeCancel = await _stripeBillingService.SetSubscriptionCancelAtPeriodEndAsync(
                        stripeSubId,
                        cancelAtPeriodEnd: true);
                    if (!stripeCancel.Succeeded)
                    {
                        TempData["Error"] = string.IsNullOrWhiteSpace(stripeCancel.Error)
                            ? "Unable to cancel Stripe subscription."
                            : stripeCancel.Error;
                        return RedirectToAction(nameof(Index));
                    }
                }

                user.IsProCancelAtPeriodEnd = true;
                user.ProSubscriptionEndsAt ??= ResolveProEndDate(user) ?? DateTime.UtcNow.AddMonths(1);
                var cancelUpdate = await _userManager.UpdateAsync(user);
                TempData[cancelUpdate.Succeeded ? "Success" : "Error"] = cancelUpdate.Succeeded
                    ? $"Pro cancellation scheduled. Access remains active until {user.ProSubscriptionEndsAt:dd MMM yyyy}."
                    : (cancelUpdate.Errors.FirstOrDefault()?.Description ?? "Unable to schedule cancellation.");

                return RedirectToAction(nameof(Index));
            }

            user.SubscriptionPlan = SubscriptionPlan.Free;
            user.IsProCancelAtPeriodEnd = false;
            var freeResult = await _userManager.UpdateAsync(user);
            TempData[freeResult.Succeeded ? "Success" : "Error"] = freeResult.Succeeded
                ? "Subscription updated to Free."
                : (freeResult.Errors.FirstOrDefault()?.Description ?? "Unable to update your subscription plan.");

            return RedirectToAction(nameof(Index));
        }

        user.SubscriptionPlan = SubscriptionPlan.Pro;
        user.IsProCancelAtPeriodEnd = false;
        if (!HasActiveProAccess(user))
        {
            user.IsProSubscriptionActive = false;
            user.ProSubscribedAt = null;
            user.ProSubscriptionEndsAt = null;
        }
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            if (plan == SubscriptionPlan.Pro && !HasActiveProAccess(user))
                TempData["Info"] = "Pro plan selected. Complete Stripe payment to activate unlimited access.";
            else
                TempData["Success"] = $"Subscription updated to {plan}.";
        }
        else
        {
            TempData["Error"] = result.Errors.FirstOrDefault()?.Description ?? "Unable to update your subscription plan.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartProCheckout(string? returnUrl)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Forbid();

        await EnsureSubscriptionWindowAsync(user);

        if (user.SubscriptionPlan != SubscriptionPlan.Pro)
        {
            TempData["Info"] = "Please choose the Pro plan first.";
            return RedirectToAction(nameof(Index));
        }

        if (HasActiveProAccess(user))
        {
            TempData["Success"] = "Your Pro subscription is already active.";
            return RedirectToAction(nameof(Index));
        }

        var successPath = Url.Action(nameof(Success), "Payment") ?? "/Payment/Success";
        var cancelPath = Url.Action(nameof(Cancel), "Payment") ?? "/Payment/Cancel";
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var successUrl = $"{baseUrl}{successPath}?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{baseUrl}{cancelPath}";

        if (!string.IsNullOrWhiteSpace(user.PendingStripeCheckoutSessionId) &&
            !string.IsNullOrWhiteSpace(user.PendingStripeCheckoutUrl))
        {
            TempData["Info"] = "Resuming your in-progress Stripe checkout.";
            return Redirect(user.PendingStripeCheckoutUrl);
        }

        var checkout = await _stripeBillingService.CreateProCheckoutSessionAsync(user, successUrl, cancelUrl);
        if (!checkout.Succeeded || string.IsNullOrWhiteSpace(checkout.CheckoutUrl) || string.IsNullOrWhiteSpace(checkout.SessionId))
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(checkout.Error)
                ? "Unable to start Stripe checkout."
                : checkout.Error;
            return RedirectToAction(nameof(Index));
        }

        user.SubscriptionPlan = SubscriptionPlan.Pro;
        user.PendingStripeCheckoutSessionId = checkout.SessionId;
        user.PendingStripeCheckoutUrl = checkout.CheckoutUrl;
        user.PendingStripeCheckoutCreatedAt = DateTime.UtcNow;

        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            TempData["Error"] = update.Errors.FirstOrDefault()?.Description ?? "Unable to save pending checkout state.";
            return RedirectToAction(nameof(Index));
        }

        return Redirect(checkout.CheckoutUrl);
    }

    public async Task<IActionResult> Success(string session_id)
    {
        var normalizedSessionId = (session_id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedSessionId))
        {
            TempData["Error"] = "Missing Stripe checkout session.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Forbid();

        if (string.Equals(
                user.LastProcessedStripeCheckoutSessionId,
                normalizedSessionId,
                StringComparison.Ordinal))
        {
            TempData["Info"] = "This Stripe checkout session was already applied.";
            return RedirectToAction(nameof(Index));
        }

        var stripeResult = await _stripeBillingService.GetCheckoutSessionAsync(normalizedSessionId);
        if (!stripeResult.Succeeded || stripeResult.Session == null)
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(stripeResult.Error)
                ? "Unable to verify Stripe payment."
                : stripeResult.Error;
            return RedirectToAction(nameof(Index));
        }

        var session = stripeResult.Session;
        if (!string.Equals(session.ClientReferenceId, user.Id, StringComparison.Ordinal))
        {
            TempData["Error"] = "Checkout session does not match the current user.";
            return RedirectToAction(nameof(Index));
        }

        var isComplete = string.Equals(session.Status, "complete", StringComparison.OrdinalIgnoreCase);
        var isPaid = string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(session.PaymentStatus, "no_payment_required", StringComparison.OrdinalIgnoreCase);

        if (!isComplete || !isPaid)
        {
            TempData["Error"] = "Stripe checkout is not completed yet.";
            return RedirectToAction(nameof(Index));
        }

        user.SubscriptionPlan = SubscriptionPlan.Pro;
        user.IsProSubscriptionActive = true;
        user.ProSubscribedAt = DateTime.UtcNow;
        user.ProSubscriptionEndsAt = user.ProSubscribedAt.Value.AddMonths(1);
        user.IsProCancelAtPeriodEnd = false;
        user.StripeCustomerId = string.IsNullOrWhiteSpace(session.CustomerId) ? user.StripeCustomerId : session.CustomerId;
        user.StripeSubscriptionId = string.IsNullOrWhiteSpace(session.SubscriptionId) ? user.StripeSubscriptionId : session.SubscriptionId;
        user.LastProcessedStripeCheckoutSessionId = string.IsNullOrWhiteSpace(session.Id)
            ? normalizedSessionId
            : session.Id;
        user.PendingStripeCheckoutSessionId = null;
        user.PendingStripeCheckoutUrl = null;
        user.PendingStripeCheckoutCreatedAt = null;

        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            TempData["Error"] = update.Errors.FirstOrDefault()?.Description ?? "Unable to activate Pro subscription.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Payment successful. Pro subscription is now active.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Cancel()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            user.PendingStripeCheckoutSessionId = null;
            user.PendingStripeCheckoutUrl = null;
            user.PendingStripeCheckoutCreatedAt = null;
            await _userManager.UpdateAsync(user);
        }

        TempData["Info"] = "Stripe checkout was canceled. You can resume payment anytime.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelPlan(string? returnUrl)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Forbid();

        await EnsureSubscriptionWindowAsync(user);
        if (!HasActiveProAccess(user))
        {
            TempData["Info"] = "You do not have an active Pro subscription to cancel.";
            return RedirectToAction(nameof(Index));
        }

        if (user.IsProCancelAtPeriodEnd)
        {
            TempData["Info"] = "Your Pro cancellation is already scheduled.";
            return RedirectToAction(nameof(Index));
        }

        var stripeSubId = user.StripeSubscriptionId?.Trim();
        if (!string.IsNullOrWhiteSpace(stripeSubId))
        {
            var stripeCancel = await _stripeBillingService.SetSubscriptionCancelAtPeriodEndAsync(
                stripeSubId,
                cancelAtPeriodEnd: true);
            if (!stripeCancel.Succeeded)
            {
                TempData["Error"] = string.IsNullOrWhiteSpace(stripeCancel.Error)
                    ? "Unable to cancel Stripe subscription."
                    : stripeCancel.Error;
                return RedirectToAction(nameof(Index));
            }
        }

        user.IsProCancelAtPeriodEnd = true;
        user.ProSubscriptionEndsAt ??= ResolveProEndDate(user) ?? DateTime.UtcNow.AddMonths(1);
        var update = await _userManager.UpdateAsync(user);
        TempData[update.Succeeded ? "Success" : "Error"] = update.Succeeded
            ? $"Pro cancellation scheduled. Access remains active until {user.ProSubscriptionEndsAt:dd MMM yyyy}."
            : (update.Errors.FirstOrDefault()?.Description ?? "Unable to cancel subscription.");

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    private static DateTime? ResolveProEndDate(ApplicationUser user)
    {
        return user.ProSubscriptionEndsAt ?? user.ProSubscribedAt?.AddMonths(1);
    }

    private static bool HasActiveProAccess(ApplicationUser user)
    {
        if (!user.IsProSubscriptionActive)
            return false;

        var endDate = ResolveProEndDate(user);
        return !endDate.HasValue || endDate.Value > DateTime.UtcNow;
    }

    private async Task ReconcilePendingCheckoutAsync(ApplicationUser user)
    {
        var pendingSessionId = user.PendingStripeCheckoutSessionId?.Trim();
        if (string.IsNullOrWhiteSpace(pendingSessionId) || HasActiveProAccess(user))
            return;

        var stripeResult = await _stripeBillingService.GetCheckoutSessionAsync(pendingSessionId);
        if (!stripeResult.Succeeded || stripeResult.Session == null)
            return;

        var session = stripeResult.Session;
        var isComplete = string.Equals(session.Status, "complete", StringComparison.OrdinalIgnoreCase);
        var isPaid = string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(session.PaymentStatus, "no_payment_required", StringComparison.OrdinalIgnoreCase);
        var isOpen = string.Equals(session.Status, "open", StringComparison.OrdinalIgnoreCase);

        if (isComplete && isPaid &&
            string.Equals(session.ClientReferenceId, user.Id, StringComparison.Ordinal))
        {
            user.SubscriptionPlan = SubscriptionPlan.Pro;
            user.IsProSubscriptionActive = true;
            user.ProSubscribedAt ??= DateTime.UtcNow;
            user.ProSubscriptionEndsAt = user.ProSubscribedAt.Value.AddMonths(1);
            user.IsProCancelAtPeriodEnd = false;
            user.StripeCustomerId = string.IsNullOrWhiteSpace(session.CustomerId) ? user.StripeCustomerId : session.CustomerId;
            user.StripeSubscriptionId = string.IsNullOrWhiteSpace(session.SubscriptionId) ? user.StripeSubscriptionId : session.SubscriptionId;
            user.LastProcessedStripeCheckoutSessionId = string.IsNullOrWhiteSpace(session.Id) ? pendingSessionId : session.Id;
            user.PendingStripeCheckoutSessionId = null;
            user.PendingStripeCheckoutUrl = null;
            user.PendingStripeCheckoutCreatedAt = null;
            await _userManager.UpdateAsync(user);
            return;
        }

        if (!isOpen)
        {
            user.PendingStripeCheckoutSessionId = null;
            user.PendingStripeCheckoutUrl = null;
            user.PendingStripeCheckoutCreatedAt = null;
            if (!HasActiveProAccess(user))
                user.SubscriptionPlan = SubscriptionPlan.Free;
            await _userManager.UpdateAsync(user);
        }
    }

    private async Task EnsureSubscriptionWindowAsync(ApplicationUser user)
    {
        var changed = false;
        var endDate = ResolveProEndDate(user);

        if (user.IsProSubscriptionActive && endDate.HasValue && endDate.Value <= DateTime.UtcNow)
        {
            user.IsProSubscriptionActive = false;
            user.IsProCancelAtPeriodEnd = false;
            user.SubscriptionPlan = SubscriptionPlan.Free;
            changed = true;
        }

        if (user.IsProSubscriptionActive && !user.ProSubscriptionEndsAt.HasValue && user.ProSubscribedAt.HasValue)
        {
            user.ProSubscriptionEndsAt = user.ProSubscribedAt.Value.AddMonths(1);
            changed = true;
        }

        if (!changed)
            return;

        await _userManager.UpdateAsync(user);
    }
}
