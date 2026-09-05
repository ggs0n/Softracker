using System.Reflection;
using System.Text.Json;
using WFHMonitor.Services;

namespace WFHMonitor.Tests;

public class CodeReadinessPaginationTests
{
    [Fact]
    public void FixFirst_UsesTenFindingsPerPage()
    {
        var viewPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "WFHMonitor", "Views", "ChangeRequest", "CodeReadiness.cshtml"));
        var content = File.ReadAllText(viewPath);

        Assert.Contains("data-fix-first-row", content);
        Assert.Contains("id=\"fixFirstPagination\"", content);
        Assert.Contains("const pageSize = 10;", content);
        Assert.Contains("Go to previous page", content);
        Assert.Contains("Go to next page", content);
    }

    [Fact]
    public void CodeReadiness_ShowsOneLineSummaryForEverySection()
    {
        var viewPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "WFHMonitor", "Views", "ChangeRequest", "CodeReadiness.cshtml"));
        var content = File.ReadAllText(viewPath);

        Assert.Contains("data-code-readiness-section-summary", content);
        Assert.Contains("Repository Readiness:</strong>", content);
        Assert.Contains("Issues Found:</strong>", content);
        Assert.Contains("SOLID Review:</strong>", content);
        Assert.Contains("OWASP Analysis:</strong>", content);
        Assert.Contains("Related Modules:</strong>", content);
        Assert.Contains("Design Patterns:</strong>", content);
        Assert.Contains("Architecture Flow:</strong>", content);
        Assert.Contains("Incomplete Work:</strong>", content);
    }

    [Fact]
    public void ScanSummary_ShowsDetectedFrontendAndBackendLanguages()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var view = File.ReadAllText(Path.Combine(
            repositoryRoot, "WFHMonitor", "Views", "ChangeRequest", "CodeReadiness.cshtml"));
        var controller = File.ReadAllText(Path.Combine(
            repositoryRoot, "WFHMonitor", "Controllers", "ChangeRequestController.cs"));

        Assert.Contains("Frontend languages", view);
        Assert.Contains("@Model.FrontendLanguages", view);
        Assert.Contains("Backend languages", view);
        Assert.Contains("@Model.BackendLanguages", view);
        Assert.Contains("DetectRepositoryLanguages(files)", controller);
    }

    [Fact]
    public void FullScan_IncludesValidationAndExceptionHandlingSections()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var view = File.ReadAllText(Path.Combine(
            repositoryRoot, "WFHMonitor", "Views", "ChangeRequest", "CodeReadiness.cshtml"));
        var service = File.ReadAllText(Path.Combine(
            repositoryRoot, "WFHMonitor", "Services", "CodexAgentService.cs"));

        Assert.Contains("Data Validation & Null Handling", view);
        Assert.Contains("Logging & Exception Handling", view);
        Assert.Contains("validationAndNullHandling", service);
        Assert.Contains("loggingAndExceptionHandling", service);
        Assert.Contains("server-side request/DTO/model validation", service);
        Assert.Contains("empty or swallowed catches", service);
    }

    [Fact]
    public void SolidReview_RequiresDetailedEvidenceAndRendersItLikeOwasp()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", ".."));
        var view = File.ReadAllText(Path.Combine(
            repositoryRoot, "WFHMonitor", "Views", "ChangeRequest", "CodeReadiness.cshtml"));
        var schemaMethod = typeof(CodexBugScanService).GetMethod(
            "BuildCodeReadinessSchema",
            BindingFlags.NonPublic | BindingFlags.Static);

        var schema = Assert.IsType<string>(schemaMethod?.Invoke(null, null));
        using var document = JsonDocument.Parse(schema);
        var solidItems = document.RootElement
            .GetProperty("properties")
            .GetProperty("solid")
            .GetProperty("items");
        var required = solidItems.GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToHashSet();

        Assert.Contains("evidence", required);
        Assert.Contains("recommendation", required);
        Assert.Contains("file", required);
        Assert.Contains("line", required);
        Assert.Contains("confidence", required);
        Assert.Contains("data-solid-analysis", view);
        Assert.Contains("SOLID Principles Analysis", view);
        Assert.Contains("<strong>Evidence:</strong> @check.Evidence", view);
        Assert.Contains("<strong>Recommendation:</strong> @check.Recommendation", view);
        Assert.Contains("View code", view);
    }
}
