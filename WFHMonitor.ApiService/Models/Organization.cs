using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WFHMonitor.Models;

public class OrgTeam
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? CompanyName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class OrganizationProfile
{
    public int Id { get; set; } = 1;

    [ForeignKey(nameof(CeoUser))]
    public string? CeoUserId { get; set; }
    public ApplicationUser? CeoUser { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
