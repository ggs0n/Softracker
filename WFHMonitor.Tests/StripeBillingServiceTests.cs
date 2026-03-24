using Microsoft.Extensions.Options;
using WFHMonitor.Services;

namespace WFHMonitor.Tests;

public class StripeBillingServiceTests
{
    [Fact]
    public void ProPriceDisplay_UsesConfiguredUnitAmount_WhenPriceIdIsNotSet()
    {
        var settings = Options.Create(new StripeBillingSettings
        {
            SecretKey = "sk_test_123",
            ProUnitAmount = 4900,
            Currency = "usd",
            ProductName = "WFHMonitor Pro"
        });

        var service = new StripeBillingService(new HttpClient(), settings);

        Assert.Equal("$49", service.ProPriceDisplay);
        Assert.Equal("per user / month", service.ProPricePeriodDisplay);
    }

    [Fact]
    public void ProPriceDisplay_UsesStripeManagedLabel_WhenPriceIdIsConfigured()
    {
        var settings = Options.Create(new StripeBillingSettings
        {
            SecretKey = "sk_test_123",
            ProPriceId = "price_123",
            ProUnitAmount = 2900,
            Currency = "usd",
            ProductName = "WFHMonitor Pro"
        });

        var service = new StripeBillingService(new HttpClient(), settings);

        Assert.Equal("Stripe-configured price", service.ProPriceDisplay);
        Assert.Equal("per user / billing cycle", service.ProPricePeriodDisplay);
    }
}
