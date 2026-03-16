using Microsoft.AspNetCore.Mvc.Rendering;
using WFHMonitor.Models;

namespace WFHMonitor.ViewModels;

public class TeamDashboardViewModel
{
    public string CeoName { get; set; } = "Me";
    public string CeoEmail { get; set; } = string.Empty;

    public List<TeamMemberNodeViewModel> Team1Members { get; set; } = new();
    public List<TeamMemberNodeViewModel> Team2Members { get; set; } = new();
    public List<TeamMemberNodeViewModel> UnassignedMembers { get; set; } = new();

    public TeamAssignmentFormViewModel AssignmentForm { get; set; } = new();
}

public class TeamMemberNodeViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Employee";
    public OrganizationTeam Team { get; set; } = OrganizationTeam.Unassigned;
}

public class TeamAssignmentFormViewModel
{
    public string MemberId { get; set; } = string.Empty;
    public OrganizationTeam Team { get; set; } = OrganizationTeam.Team1;
    public List<SelectListItem> MemberOptions { get; set; } = new();
}
