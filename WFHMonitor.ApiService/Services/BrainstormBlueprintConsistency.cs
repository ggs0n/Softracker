using System.Text.RegularExpressions;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public static class BrainstormBlueprintConsistency
{
    public static BrainstormDesignBlueprint Apply(
        BrainstormDesignBlueprint blueprint,
        BrainstormGenerateDesignRequest request) =>
        Apply(blueprint, request.Features);

    public static BrainstormDesignBlueprint Apply(
        BrainstormDesignBlueprint blueprint,
        string features)
    {
        var capabilities = ExtractCapabilities(features);
        if (capabilities.Count == 0) return blueprint;

        var components = (blueprint.MainComponents ?? []).ToList();
        var services = (blueprint.ServiceDetails ?? []).ToList();
        var endpoints = (blueprint.RestEndpoints ?? []).ToList();
        var schemas = (blueprint.TableSchemas ?? []).ToList();
        var risks = (blueprint.RisksTradeoffs ?? []).ToList();
        var nextSteps = (blueprint.NextSteps ?? []).ToList();
        var domainDeployment = blueprint.DeploymentServices?
            .FirstOrDefault(item =>
                item.Module.Contains(
                    "Domain",
                    StringComparison.OrdinalIgnoreCase));

        foreach (var capability in capabilities)
        {
            if (!Contains(components, capability))
                components.Add($"{capability} capability");

            if (!services.Any(item => Contains(
                    [
                        item.Name,
                        item.Responsibility,
                        item.DataOwnership,
                        .. item.Dependencies,
                        .. item.Communication
                    ],
                    capability)))
            {
                services.Add(new(
                    $"{capability} Module",
                    "Domain module",
                    $"Implements the complete {capability} workflow and its business rules.",
                    domainDeployment?.Runtime ?? ".NET 8",
                    domainDeployment?.RecommendedService
                        ?? "ASP.NET Core Domain Modules",
                    $"Owns authoritative {capability} records and publishes changes as events.",
                    ["Backend API", "Database"],
                    ["In-process calls initially", "REST", "Asynchronous events"]));
            }

            if (!endpoints.Any(item => Contains(
                    [
                        item.Path,
                        item.Purpose,
                        item.Request,
                        item.Response
                    ],
                    capability)))
            {
                AddRestEndpoints(endpoints, capability);
            }

            if (!schemas.Any(item => Contains(
                    [
                        item.Name,
                        item.Purpose,
                        .. item.Columns.Select(column => column.Notes),
                        .. item.Relationships
                    ],
                    capability)))
            {
                schemas.Add(BuildSchema(capability));
            }

            if (!Contains(risks, capability))
            {
                risks.Add(
                    $"{capability}: confirm authorization, validation, failure handling, audit, and retention requirements.");
            }

            if (!Contains(nextSteps, capability))
                nextSteps.Add($"Implement and validate one complete {capability} workflow.");
        }

        var scope = string.Join(", ", capabilities);
        var recommendedArchitecture = Contains(
                [blueprint.RecommendedArchitecture],
                capabilities)
            ? blueprint.RecommendedArchitecture
            : $"{blueprint.RecommendedArchitecture.TrimEnd()} Canonical domain capabilities: {scope}.";
        var apiRecommendation = Contains(
                [blueprint.ApiBackendRecommendation],
                capabilities)
            ? blueprint.ApiBackendRecommendation
            : $"{blueprint.ApiBackendRecommendation.TrimEnd()} Keep versioned contracts aligned to these capabilities: {scope}.";
        var diagrams = blueprint.Diagrams
            .Select(diagram => diagram with
            {
                Mermaid = AddCapabilityComments(
                    diagram.Mermaid,
                    capabilities)
            })
            .ToArray();

        return blueprint with
        {
            RecommendedArchitecture = recommendedArchitecture,
            MainComponents = components,
            ServiceDetails = services,
            RestEndpoints = endpoints,
            TableSchemas = schemas,
            RisksTradeoffs = risks,
            NextSteps = nextSteps,
            ApiBackendRecommendation = apiRecommendation,
            Diagrams = diagrams
        };
    }

    public static IReadOnlyList<string> ExtractCapabilities(string features) =>
        (features ?? string.Empty)
            .Split(
                [',', ';', '\n', '\r'],
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .Select(value =>
                Regex.Replace(value, @"\s+", " ").Trim())
            .Where(value => value.Length > 0)
            .Select(Capitalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();

    private static void AddRestEndpoints(
        ICollection<BrainstormRestEndpoint> endpoints,
        string capability)
    {
        var resource = Slug(capability);
        var contract = ContractName(capability);
        endpoints.Add(new(
            $"{capability} Module",
            "GET",
            $"/api/v1/{resource}",
            $"Lists or searches {capability} records visible to the caller.",
            "Authenticated user with record ownership or administrator role.",
            "Paging, filtering, and sorting query parameters.",
            $"PagedResult<{contract}Response> JSON",
            ["200", "400", "401", "403"]));
        endpoints.Add(new(
            $"{capability} Module",
            "POST",
            $"/api/v1/{resource}",
            $"Creates a {capability} record through its canonical workflow.",
            "Authenticated user with create permission.",
            $"Create{contract}Request JSON",
            $"{contract}Response JSON with Location header",
            ["201", "400", "401", "403", "409", "422"]));
    }

    private static BrainstormTableSchema BuildSchema(string capability)
    {
        var table = $"{Snake(capability)}_records";
        return new(
            table,
            $"Stores authoritative {capability} records.",
            [
                new("id", "uuid", true, false,
                    $"Unique {capability} record identifier."),
                new("owner_user_id", "uuid", false, true,
                    $"User who owns the {capability} record."),
                new("status", "varchar(40)", false, false,
                    $"Current {capability} workflow status."),
                new("created_at", "datetime", false, false,
                    "UTC creation timestamp."),
                new("updated_at", "datetime", false, false,
                    "UTC last-change timestamp.")
            ],
            ["owner_user_id -> users.id"]);
    }

    private static string AddCapabilityComments(
        string mermaid,
        IReadOnlyList<string> capabilities)
    {
        var missing = capabilities
            .Where(capability => !mermaid.Contains(
                capability,
                StringComparison.OrdinalIgnoreCase))
            .Select(capability => $"%% Capability: {capability}")
            .ToArray();
        return missing.Length == 0
            ? mermaid
            : $"{mermaid.TrimEnd()}\n{string.Join('\n', missing)}";
    }

    private static bool Contains(
        IEnumerable<string> values,
        string capability) =>
        values.Any(value => value?.Contains(
            capability,
            StringComparison.OrdinalIgnoreCase) == true);

    private static bool Contains(
        IEnumerable<string> values,
        IEnumerable<string> capabilities) =>
        capabilities.All(capability => Contains(values, capability));

    private static string Capitalize(string value) =>
        value.Length == 0
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];

    private static string Slug(string value) =>
        Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "-")
            .Trim('-') is { Length: > 0 } slug
            ? slug
            : "capability";

    private static string Snake(string value) =>
        Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "_")
            .Trim('_') is { Length: > 0 } snake
            ? snake
            : "capability";

    private static string ContractName(string value)
    {
        var words = Regex.Split(value, @"[^a-zA-Z0-9]+")
            .Where(word => word.Length > 0)
            .Select(word =>
                char.ToUpperInvariant(word[0])
                + word[1..].ToLowerInvariant());
        var result = string.Concat(words);
        return result.Length > 0 ? result : "Capability";
    }
}
