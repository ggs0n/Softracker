namespace WFHMonitor.Tests;

public class PaymentViewRegressionTests
{
    [Fact]
    public void PaymentIndex_IncludesSingleSubmitCheckoutGuard()
    {
        var viewPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "WFHMonitor", "Views", "Payment", "Index.cshtml");
        var content = File.ReadAllText(Path.GetFullPath(viewPath));

        Assert.Contains("js-stripe-checkout-form", content);
        Assert.Contains("form.dataset.submitted = 'true'", content);
        Assert.Contains("Redirecting to Stripe...", content);
        Assert.Contains("button.disabled = true", content);
    }
}
