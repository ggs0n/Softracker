using System.ComponentModel.DataAnnotations;

namespace WFHMonitor.ViewModels;

public class CalendarEventCreateViewModel
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Details { get; set; }

    [MaxLength(1000)]
    [Display(Name = "Meeting Link")]
    [Url(ErrorMessage = "Please provide a valid URL.")]
    public string? MeetingLink { get; set; }

    [Required]
    [Display(Name = "Start")]
    public string StartAt { get; set; } = string.Empty;

    [Display(Name = "End")]
    public string? EndAt { get; set; }
}
