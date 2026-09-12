using System.ComponentModel.DataAnnotations;

namespace WFHMonitor.Models;

public class ProjectKickStartDesign
{
    public int Id { get; set; }

    [MaxLength(180)]
    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string Technology { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? CloudHostingTarget { get; set; }

    [MaxLength(100)]
    public string UserCount { get; set; } = string.Empty;

    public string Features { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? UiDirection { get; set; }

    public string BlueprintJson { get; set; } = string.Empty;

    [MaxLength(40)]
    public string SourceMode { get; set; } = "Rules-based";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    public string CreatedById { get; set; } = string.Empty;

    public ApplicationUser? CreatedBy { get; set; }
}
