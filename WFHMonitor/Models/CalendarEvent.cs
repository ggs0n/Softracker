using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WFHMonitor.Models;

public class CalendarEvent
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Details { get; set; }

    [MaxLength(1000)]
    [Display(Name = "Meeting Link")]
    public string? MeetingLink { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "Start")]
    public DateTime StartAt { get; set; }

    [DataType(DataType.DateTime)]
    [Display(Name = "End")]
    public DateTime? EndAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(CreatedBy))]
    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }
}
