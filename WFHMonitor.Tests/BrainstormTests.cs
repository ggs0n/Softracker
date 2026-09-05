using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Moq;
using WFHMonitor.Services;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Tests;

public sealed class BrainstormTests
{
    [Fact]
    public async Task Generator_UsesCompleteFallbackWhenAutomationIsUnavailable()
    {
        var automation = new Mock<IAiAutomationService>();
        automation
            .Setup(service => service.GenerateBrainstormAsync(
                It.IsAny<BrainstormGenerateDesignRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Automation unavailable."));
        var generator = new BrainstormDesignGenerator(
            automation.Object,
            NullLogger<BrainstormDesignGenerator>.Instance);

        var result = await generator.GenerateAsync(
            new(
                "Appointment booking platform",
                "React, ASP.NET Core, SQL Server, Azure",
                "25,000 users",
                "Booking, reminders, payments"),
            CancellationToken.None);

        Assert.Equal("Fallback", result.SourceMode);
        Assert.Equal(5, result.Diagrams.Count);
        Assert.True(result.DeploymentServices?.Count >= 4);
        Assert.True(result.TableSchemas?.Count >= 3);
        Assert.True(result.ServiceDetails?.Count >= 2);
        Assert.True(result.RestEndpoints?.Count >= 1);
        Assert.NotEmpty(result.GrpcContracts ?? []);
        Assert.NotEmpty(result.BatchJobs ?? []);
        Assert.NotNull(result.CostEstimate);
    }

    [Fact]
    public void Importer_ExtractsFencedJsonAndAddsRequiredDiagrams()
    {
        var source = new BrainstormDesignBlueprint(
            "Booking",
            "Modular architecture",
            ["Client", "API", "Database"],
            [],
            "SQL",
            "REST",
            "Scale horizontally",
            "Use OIDC",
            null,
            ["Complex integrations"],
            ["Build a vertical slice"],
            [],
            [
                new("Context", "context", "flowchart LR\n A-->B"),
                new("Components", "components", "flowchart TB\n A-->B"),
                new("Flow", "dataFlow",
                    "sequenceDiagram\n A->>B: request")
            ],
            "ChatGPT Plus",
            null);
        var json = JsonSerializer.Serialize(
            source,
            BrainstormJsonOptions.Default);

        IBrainstormResultImporter importer = new BrainstormResultImporter();

        var imported = importer.Import(
            $"```json\n{json}\n```");

        Assert.Equal("ChatGPT Plus Import", imported.SourceMode);
        Assert.Equal(5, imported.Diagrams.Count);
        Assert.Contains(
            imported.Diagrams,
            diagram => diagram.Kind == "detailedArchitecture");
        Assert.Contains(
            imported.Diagrams,
            diagram => diagram.Kind == "cloudDeploymentTemplate");
        Assert.Empty(imported.ServiceDetails ?? []);
        Assert.Empty(imported.RestEndpoints ?? []);
        Assert.Empty(imported.GrpcContracts ?? []);
        Assert.Empty(imported.BatchJobs ?? []);
    }

    [Fact]
    public void Validator_RejectsMissingAndOversizedBriefValues()
    {
        var request = new BrainstormGenerateDesignRequest(
            string.Empty,
            new string('x', 2_001),
            string.Empty,
            string.Empty,
            new string('x', 121));

        var errors = BrainstormRequestValidator.Validate(request);

        Assert.Contains(nameof(request.Summary), errors.Keys);
        Assert.Contains(nameof(request.Technology), errors.Keys);
        Assert.Contains(nameof(request.CloudHostingTarget), errors.Keys);
        Assert.Contains(nameof(request.UserCount), errors.Keys);
        Assert.Contains(nameof(request.Features), errors.Keys);
    }

    [Fact]
    public void BlueprintSchema_RequiresServiceAndInterfaceDetails()
    {
        var required = BrainstormBlueprintSchema.Value["required"]!
            .AsArray()
            .Select(item => item!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("serviceDetails", required);
        Assert.Contains("restEndpoints", required);
        Assert.Contains("grpcContracts", required);
        Assert.Contains("batchJobs", required);
    }

    [Fact]
    public async Task Generator_KeepsCanonicalCapabilitiesAcrossAllSections()
    {
        var automation = new Mock<IAiAutomationService>();
        automation
            .Setup(service => service.GenerateBrainstormAsync(
                It.IsAny<BrainstormGenerateDesignRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(
                "Automation unavailable."));
        var generator = new BrainstormDesignGenerator(
            automation.Object,
            NullLogger<BrainstormDesignGenerator>.Instance);

        var result = await generator.GenerateAsync(
            new(
                "Customer portal",
                "React and .NET 8",
                "10,000",
                "Registration, Payments"),
            CancellationToken.None);

        foreach (var capability in new[] { "Registration", "Payments" })
        {
            Assert.Contains(
                capability,
                result.RecommendedArchitecture,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                result.MainComponents,
                item => item.Contains(
                    capability,
                    StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                result.ServiceDetails ?? [],
                item => item.Name.Contains(
                    capability,
                    StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                result.RestEndpoints ?? [],
                item => item.Purpose.Contains(
                    capability,
                    StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                result.TableSchemas ?? [],
                item =>
                    item.Purpose.Contains(
                        capability,
                        StringComparison.OrdinalIgnoreCase)
                    || item.Columns.Any(column =>
                        column.Notes.Contains(
                            capability,
                            StringComparison.OrdinalIgnoreCase)));
            Assert.Contains(
                result.RisksTradeoffs,
                item => item.Contains(
                    capability,
                    StringComparison.OrdinalIgnoreCase));
            Assert.Contains(
                result.NextSteps,
                item => item.Contains(
                    capability,
                    StringComparison.OrdinalIgnoreCase));
            Assert.All(
                result.Diagrams,
                diagram => Assert.Contains(
                    capability,
                    diagram.Mermaid,
                    StringComparison.OrdinalIgnoreCase));
        }
    }
}
