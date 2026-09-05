using WFHMonitor.Services;

namespace WFHMonitor.Tests;

public class FeatureDetectorTests
{
    [Fact]
    public void DetectModules_FindsTvRepairDomainModulesFromRepositoryPaths()
    {
        var paths = new[]
        {
            "TVRepair.Api/apicontroller/AuthenticationController.cs",
            "TVRepair.Api/apicontroller/PaymentController.cs",
            "TVRepair.Api/apicontroller/TVRepairController.cs",
            "TVRepair.Api/model/RepairOrder.cs",
            "TVRepair.Api/model/Quotation.cs",
            "TVRepair.Web/src/pages/CustomerPage.jsx",
            "TVRepair.Web/src/pages/Register.jsx",
            "TVRepair.Web/src/pages/TechnicianAccepted.jsx",
            "TVRepair.Web/src/pages/TechnicianPage.jsx",
            "TVRepair.Web/src/pages/CheckStatus.jsx"
        };

        var modules = FeatureDetector.DetectModules(paths).Select(module => module.Name).ToHashSet();

        Assert.Contains("Authentication", modules);
        Assert.Contains("Register", modules);
        Assert.Contains("Customer", modules);
        Assert.Contains("Technician", modules);
        Assert.Contains("Payment", modules);
        Assert.Contains("TV Repair", modules);
        Assert.Contains("Quotation", modules);
        Assert.Contains("Check Status", modules);
    }

    [Fact]
    public void DetectModules_KeepsEvidencePathsForEachModule()
    {
        var modules = FeatureDetector.DetectModules(
        [
            "TVRepair.Web/src/pages/CustomerPage.jsx",
            "TVRepair.Api/data/CustomerRequest.cs"
        ]);

        var customer = Assert.Single(modules, module => module.Name == "Customer");
        Assert.Contains(customer.EvidencePaths, path => path.Contains("CustomerPage.jsx"));
        Assert.Contains(customer.EvidencePaths, path => path.Contains("CustomerRequest.cs"));
    }

    [Fact]
    public void DetectModules_FindsWeddingBookingDomainAndIgnoresDefaultWeatherForecast()
    {
        var paths = new[]
        {
            "WeddingBooking.Api/Controllers/WeatherForecastController.cs",
            "WeddingBooking.Api/Models/Booking.cs",
            "WeddingBooking.Api/Models/Wedding.cs",
            "WeddingBooking.Api/Models/GuestRsvp.cs",
            "WeddingBooking.Api/Models/Venue.cs",
            "WeddingBooking.Api/Models/Vendor.cs",
            "WeddingBooking.Api/Models/WeddingPackage.cs",
            "WeddingBooking.Api/Services/PaymentService.cs"
        };

        var modules = FeatureDetector.DetectModules(paths).Select(module => module.Name).ToHashSet();

        Assert.Contains("Booking", modules);
        Assert.Contains("Wedding", modules);
        Assert.Contains("Guest Rsvp", modules);
        Assert.Contains("Venue", modules);
        Assert.Contains("Vendor", modules);
        Assert.Contains("Wedding Package", modules);
        Assert.Contains("Payment", modules);
        Assert.DoesNotContain("Weather Forecast", modules);
    }

    [Fact]
    public void DetectModules_DiscoversUnknownDomainsWithoutAConfiguredPattern()
    {
        var paths = new[]
        {
            "Commerce.Api/Controllers/InventoryController.cs",
            "Commerce.Api/Services/ShipmentService.cs",
            "Commerce.Api/Models/LoyaltyProgram.cs",
            "Commerce.Web/Views/Returns/Index.cshtml"
        };

        var modules = FeatureDetector.DetectModules(paths).Select(module => module.Name).ToHashSet();

        Assert.Contains("Inventory", modules);
        Assert.Contains("Shipment", modules);
        Assert.Contains("Loyalty Program", modules);
        Assert.Contains("Return", modules);
    }

    [Fact]
    public void DetectModules_MergesTechnicalVariantsAndIgnoresCrudPages()
    {
        var paths = new[]
        {
            "MoneyTracker.Domain/Entities/DebtAccount.cs",
            "MoneyTracker.Web/Controllers/DebtAccountsController.cs",
            "MoneyTracker.Web/ViewModels/DebtAccountViewModel.cs",
            "MoneyTracker.Web/Views/DebtAccounts/Create.cshtml",
            "MoneyTracker.Web/Views/DebtAccounts/Edit.cshtml",
            "MoneyTracker.Web/Controllers/DashboardController.cs",
            "MoneyTracker.Web/ViewModels/DashboardViewModel.cs",
            "MoneyTracker.Application/Services/IEmailService.cs",
            "MoneyTracker.Infrastructure/Services/EmailService.cs"
        };

        var modules = FeatureDetector.DetectModules(paths);

        var debtAccount = Assert.Single(modules, module => module.Name == "Debt Account");
        Assert.Equal(5, debtAccount.EvidencePaths.Count);
        Assert.Single(modules, module => module.Name == "Dashboard");
        Assert.Single(modules, module => module.Name == "Email");
        Assert.DoesNotContain(modules, module => module.Name is "Create" or "Edit" or "Dashboard View" or "Debt Accounts");
    }
}
