using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Models;

public sealed class BrainstormDesignProject
{
    public int Id { get; set; }

    [MaxLength(180)]
    public required string Title { get; set; }

    public required string Summary { get; set; }

    public required string Technology { get; set; }

    [MaxLength(120)]
    public string? CloudHostingTarget { get; set; }

    [MaxLength(100)]
    public required string UserCount { get; set; }

    public required string Features { get; set; }

    public required string BlueprintJson { get; set; }

    [MaxLength(40)]
    public required string SourceMode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    [MaxLength(450)]
    public required string CreatedById { get; set; }

    public ApplicationUser? CreatedBy { get; set; }

    public static BrainstormDesignProject From(
        BrainstormGenerateDesignRequest request,
        BrainstormDesignBlueprint blueprint,
        string createdById) =>
        new()
        {
            Title = blueprint.Title,
            Summary = request.Summary,
            Technology = request.Technology,
            CloudHostingTarget = string.IsNullOrWhiteSpace(request.CloudHostingTarget)
                ? null
                : request.CloudHostingTarget,
            UserCount = request.UserCount,
            Features = request.Features,
            BlueprintJson = JsonSerializer.Serialize(
                blueprint,
                BrainstormJsonOptions.Default),
            SourceMode = blueprint.SourceMode,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedById = createdById
        };
}
