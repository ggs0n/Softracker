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

    public PaymentController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        IStripeBillingService stripeBillingService)
    {
        _userManager = userManager;
        _db = db;
        _stripeBillingService = stripeBillingService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return Forbid();

        var vm = new PaymentPlansViewModel
        {
            CurrentPlan = user.SubscriptionPlan,
            IsProSubscriptionActive = user.IsProSubscriptionActive,
            ProSubscribedAt = user.ProSubscribedAt,
            IsStripeBillingConfigured = _stripeBillingService.IsConfigured,
            CurrentCrCount = await _db.ChangeRequests.CountAsync(c => c.CreatedById == userId),
            CurrentBugCount = await _db.BugReports.CountAsync(b => b.CreatedById == userId)
        };

        ViewData["Title"] = "Payment";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChoosePlan(SubscriptionPlan plan, string? returnUrl)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Forbid();

        user.SubscriptionPlan = plan;
        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            if (plan == SubscriptionPlan.Pro && !user.IsProSubscriptionActive)
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

        if (user.SubscriptionPlan != SubscriptionPlan.Pro)
        {
            TempData["Info"] = "Please choose the Pro plan first.";
            return RedirectToAction(nameof(Index));
        }

        if (user.IsProSubscriptionActive)
        {
            TempData["Success"] = "Your Pro subscription is already active.";
            return RedirectToAction(nameof(Index));
        }

        var successPath = Url.Action(nameof(Success), "Payment") ?? "/Payment/Success";
        var cancelPath = Url.Action(nameof(Cancel), "Payment") ?? "/Payment/Cancel";
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var successUrl = $"{baseUrl}{successPath}?session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{baseUrl}{cancelPath}";

        var checkout = await _stripeBillingService.CreateProCheckoutSessionAsync(user, successUrl, cancelUrl);
        if (!checkout.Succeeded || string.IsNullOrWhiteSpace(checkout.CheckoutUrl))
        {
            TempData["Error"] = string.IsNullOrWhiteSpace(checkout.Error)
                ? "Unable to start Stripe checkout."
                : checkout.Error;
            return RedirectToAction(nameof(Index));
        }

        return Redirect(checkout.CheckoutUrl);
    }

    public async Task<IActionResult> Success(string session_id)
    {
        if (string.IsNullOrWhiteSpace(session_id))
        {
            TempData["Error"] = "Missing Stripe checkout session.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
            return Forbid();

        var stripeResult = await _stripeBillingService.GetCheckoutSessionAsync(session_id);
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
        user.ProSubscribedAt ??= DateTime.UtcNow;
        user.StripeCustomerId = string.IsNullOrWhiteSpace(session.CustomerId) ? user.StripeCustomerId : session.CustomerId;
        user.StripeSubscriptionId = string.IsNullOrWhiteSpace(session.SubscriptionId) ? user.StripeSubscriptionId : session.SubscriptionId;

        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            TempData["Error"] = update.Errors.FirstOrDefault()?.Description ?? "Unable to activate Pro subscription.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Payment successful. Pro subscription is now active.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Cancel()
    {
        TempData["Info"] = "Stripe checkout was canceled. You can resume payment anytime.";
        return RedirectToAction(nameof(Index));
    }
}
