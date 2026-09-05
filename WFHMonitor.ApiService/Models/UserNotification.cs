using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WFHMonitor.Models;

public class UserNotification
{
    public int Id { get; set; }

    [Required, ForeignKey(nameof(Recipient))]
    public string RecipientId { get; set; } = string.Empty;
    public ApplicationUser? Recipient { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
