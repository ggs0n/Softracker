using System.Text.Json;
using System.Text.RegularExpressions;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public sealed partial class BrainstormResultImporter : IBrainstormResultImporter
{
    private static readonly string[] RequiredDiagramKinds =
    [
        "context",
        "components",
        "dataFlow",
        "detailedArchitecture",
        "cloudDeploymentTemplate"
    ];

    public BrainstormDesignBlueprint Import(string rawResult)
    {
        var json = ExtractJson(rawResult);
        var blueprint = JsonSerializer.Deserialize<BrainstormDesignBlueprint>(
            json,
            BrainstormJsonOptions.Default)
            ?? throw new InvalidOperationException(
                "The ChatGPT result could not be parsed.");

        if (blueprint.Diagrams is null || blueprint.Diagrams.Count < 3)
            throw new InvalidOperationException(
                "The ChatGPT result must include at least three Mermaid diagrams.");

        var diagrams = blueprint.Diagrams.ToList();
        AddMissingDiagrams(diagrams);

        var requiredSet = RequiredDiagramKinds.ToHashSet(StringComparer.Ordinal);
        var ordered = RequiredDiagramKinds
            .SelectMany(kind =>
                diagrams.Where(diagram => diagram.Kind == kind).Take(1))
            .Concat(diagrams.Where(diagram => !requiredSet.Contains(diagram.Kind)))
            .Take(5)
            .ToArray();

        return blueprint with
        {
            Diagrams = ordered,
            DeploymentServices = blueprint.DeploymentServices ?? [],
            TableSchemas = blueprint.TableSchemas ?? [],
            ServiceDetails = blueprint.ServiceDetails ?? [],
            RestEndpoints = blueprint.RestEndpoints ?? [],
            GrpcContracts = blueprint.GrpcContracts ?? [],
            BatchJobs = blueprint.BatchJobs ?? [],
            SourceMode = "ChatGPT Plus Import",
            Notice =
                "Imported from a ChatGPT response pasted into the Brainstorm workspace."
        };
    }

    private static void AddMissingDiagrams(
        ICollection<BrainstormDesignDiagram> diagrams)
    {
        if (diagrams.All(diagram => diagram.Kind != "detailedArchitecture"))
        {
            diagrams.Add(new(
                "Detailed Architecture",
                "detailedArchitecture",
                """
                flowchart LR
                  Client[Client App] --> Auth{Authentication}
                  Auth -->|Verified| Api[Backend API]
                  Auth -.->|Rejected| Rejected((Rejected))
                  Api --> Core[Domain Modules]
                  Core --> Db[(Database)]
                  Core --> Worker[Background Worker]
                """));
        }

        if (diagrams.All(diagram => diagram.Kind != "cloudDeploymentTemplate"))
        {
            diagrams.Add(new(
                "Cloud Deployment Template",
                "cloudDeploymentTemplate",
                """
                flowchart LR
                  subgraph Cloud[Cloud / Hosting Environment]
                    User([User]) --> UI[React Client]
                    UI --> Auth[Identity Provider]
                    UI --> API[.NET API]
                    API --> Modules[Domain Modules]
                    Modules --> DB[(Database)]
                    Modules --> Queue[Queue / Cache]
                    Queue --> Worker[Worker]
                  end
                """));
        }
    }

    private static string ExtractJson(string rawResult)
    {
        var trimmed = rawResult.Trim();
        var fenced = JsonFenceRegex().Match(trimmed);
        if (fenced.Success)
            return fenced.Groups["json"].Value.Trim();

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        return firstBrace >= 0 && lastBrace > firstBrace
            ? trimmed[firstBrace..(lastBrace + 1)]
            : trimmed;
    }

    [GeneratedRegex(
        @"```(?:json)?\s*(?<json>\{[\s\S]*?\})\s*```",
        RegexOptions.IgnoreCase)]
    private static partial Regex JsonFenceRegex();
}
