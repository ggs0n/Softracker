using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class BugFormViewModel
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    [MaxLength(4000)]
    public string? Workflow { get; set; }

    [MaxLength(4000)]
    [Display(Name = "Steps To Reproduce")]
    public string? StepsToReproduce { get; set; }

    [MaxLength(200)]
    [Display(Name = "Module Impacted")]
    public string? ModuleImpacted { get; set; }

    public BugStatus Status { get; set; } = BugStatus.New;
    public BugAssigneeType AssigneeType { get; set; } = BugAssigneeType.Developer;
    public BugAgentStatus AgentStatus { get; set; } = BugAgentStatus.None;

    [Display(Name = "Assign To Developer")]
    public string? AssignedDeveloperId { get; set; }

    public List<SelectListItem> DeveloperOptions { get; set; } = new();

    [Display(Name = "Change Request")]
    public int? ChangeRequestId { get; set; }

    [MaxLength(300)]
    [Display(Name = "Change Request (Free Text)")]
    public string? ChangeRequestReferenceText { get; set; }

    public List<SelectListItem> ChangeRequestOptions { get; set; } = new();

    [Display(Name = "Supporting Document")]
    public Microsoft.AspNetCore.Http.IFormFile? DocumentFile { get; set; }
}
