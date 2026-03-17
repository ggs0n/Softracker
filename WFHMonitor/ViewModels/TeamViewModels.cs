using Microsoft.AspNetCore.Mvc.Rendering;

namespace WFHMonitor.ViewModels;

public class TeamDashboardViewModel
{
    public string CeoName { get; set; } = "Me";
    public string CeoEmail { get; set; } = string.Empty;

    public List<OrganizationTeamCardViewModel> Teams { get; set; } = new();
    public List<TeamMemberNodeViewModel> UnassignedMembers { get; set; } = new();
    public List<ProjectTeamNodeViewModel> UnassignedProjects { get; set; } = new();

    public TeamAssignmentFormViewModel AssignmentForm { get; set; } = new();
    public ProjectTeamAssignmentFormViewModel ProjectAssignmentForm { get; set; } = new();
    public TeamCreateFormViewModel TeamCreateForm { get; set; } = new();
    public TeamRenameFormViewModel TeamRenameForm { get; set; } = new();
    public TeamDeleteFormViewModel TeamDeleteForm { get; set; } = new();
    public TeamCeoFormViewModel CeoForm { get; set; } = new();
}

public class OrganizationTeamCardViewModel
{
    public int TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public List<TeamMemberNodeViewModel> Members { get; set; } = new();
    public List<ProjectTeamNodeViewModel> Projects { get; set; } = new();
}

public class TeamMemberNodeViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Employee";
    public int? TeamId { get; set; }
}

public class ProjectTeamNodeViewModel
{
    public int ProjectId { get; set; }
    public string CrNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? TeamId { get; set; }
}

public class TeamAssignmentFormViewModel
{
    public string MemberId { get; set; } = string.Empty;
    public int? TeamId { get; set; }
    public List<SelectListItem> MemberOptions { get; set; } = new();
    public List<SelectListItem> TeamOptions { get; set; } = new();
}

public class ProjectTeamAssignmentFormViewModel
{
    public int? ProjectId { get; set; }
    public int? TeamId { get; set; }
    public List<SelectListItem> ProjectOptions { get; set; } = new();
    public List<SelectListItem> TeamOptions { get; set; } = new();
}

public class TeamCreateFormViewModel
{
    public string Name { get; set; } = string.Empty;
}

public class TeamRenameFormViewModel
{
    public int TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<SelectListItem> TeamOptions { get; set; } = new();
}

public class TeamDeleteFormViewModel
{
    public int TeamId { get; set; }
    public List<SelectListItem> TeamOptions { get; set; } = new();
}

public class TeamCeoFormViewModel
{
    public string CeoUserId { get; set; } = string.Empty;
    public List<SelectListItem> CeoOptions { get; set; } = new();
}
