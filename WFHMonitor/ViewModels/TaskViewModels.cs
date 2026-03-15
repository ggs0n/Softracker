using System.ComponentModel.DataAnnotations;
using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class TaskCreateViewModel : IValidatableObject
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Details { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    public string? AssigneeId { get; set; }

    public WorkTaskStatus Status { get; set; } = WorkTaskStatus.ToDo;

    [DataType(DataType.Date)]
    [Display(Name = "Timeline Start")]
    public DateTime? TimelineStart { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Timeline End")]
    public DateTime? TimelineEnd { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Due Date")]
    public DateTime? DueDate { get; set; }

    [Display(Name = "Related Project")]
    public int? ChangeRequestId { get; set; }

    [Display(Name = "Related Bug")]
    public int? BugReportId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TimelineStart.HasValue && TimelineEnd.HasValue && TimelineEnd.Value.Date < TimelineStart.Value.Date)
        {
            yield return new ValidationResult(
                "Timeline End cannot be earlier than Timeline Start.",
                new[] { nameof(TimelineEnd) });
        }

        if (DueDate.HasValue && TimelineStart.HasValue && DueDate.Value.Date < TimelineStart.Value.Date)
        {
            yield return new ValidationResult(
                "Due Date cannot be earlier than Timeline Start.",
                new[] { nameof(DueDate) });
        }
    }
}

public class TaskEditViewModel : TaskCreateViewModel
{
    public int Id { get; set; }
}

public class TaskBoardViewModel
{
    public List<WorkTask> ToDo { get; set; } = new();
    public List<WorkTask> InProgress { get; set; } = new();
    public List<WorkTask> Blocked { get; set; } = new();
    public List<WorkTask> Done { get; set; } = new();

    public IEnumerable<WorkTask> AssignedTasks =>
        ToDo.Concat(InProgress).Concat(Blocked).Concat(Done).OrderBy(t => t.TimelineEnd ?? t.DueDate ?? DateTime.MaxValue);
}
