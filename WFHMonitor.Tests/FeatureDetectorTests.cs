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

        Assert.Contains("Authentication & Registration", modules);
        Assert.Contains("Customer", modules);
        Assert.Contains("Technician", modules);
        Assert.Contains("Payment", modules);
        Assert.Contains("TV Repair", modules);
        Assert.Contains("Quotation", modules);
        Assert.Contains("Status Tracking", modules);
    }

    [Fact]
    public void DetectModules_KeepsEvidencePathsForEachModule()
    {
        var modules = FeatureDetector.DetectModules(
        [
            "TVRepair.Web/src/pages/CustomerPage.jsx",
            "TVRepair.Api/data/CustRegisterRequest.cs"
        ]);

        var customer = Assert.Single(modules, module => module.Name == "Customer");
        Assert.Contains(customer.EvidencePaths, path => path.Contains("CustomerPage.jsx"));
        Assert.Contains(customer.EvidencePaths, path => path.Contains("CustRegisterRequest.cs"));
    }
}
