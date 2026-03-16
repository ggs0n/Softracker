using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using WFHMonitor.Models;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin")]
public class TeamController : Controller
{
    private static readonly string[] TeamRoles = ["Employee", "Developer", "Tester", "Agent"];

    private readonly UserManager<ApplicationUser> _userManager;

    public TeamController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        return View(await BuildDashboardAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignMemberTeam([Bind(Prefix = "AssignmentForm")] TeamAssignmentFormViewModel model)
    {
        model.MemberId = (model.MemberId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(model.MemberId))
            ModelState.AddModelError("AssignmentForm.MemberId", "Please select a team member.");

        var member = string.IsNullOrWhiteSpace(model.MemberId) ? null : await _userManager.FindByIdAsync(model.MemberId);
        if (member == null)
            ModelState.AddModelError("AssignmentForm.MemberId", "Selected member not found.");

        if (member != null && await _userManager.IsInRoleAsync(member, "Admin"))
            ModelState.AddModelError("AssignmentForm.MemberId", "Admin account cannot be assigned to Team 1 or Team 2.");

        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildDashboardAsync(model));

        member!.OrganizationTeam = model.Team;
        var update = await _userManager.UpdateAsync(member);
        if (!update.Succeeded)
        {
            TempData["Error"] = update.Errors.FirstOrDefault()?.Description ?? "Unable to assign team.";
            return View(nameof(Index), await BuildDashboardAsync(model));
        }

        TempData["Success"] = $"{member.FullName} assigned to {GetTeamLabel(model.Team)}.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Agent()
    {
        return RedirectToAction("Index", "Agent");
    }

    public IActionResult Employees()
    {
        return RedirectToAction("Employees", "Admin");
    }

    private async Task<TeamDashboardViewModel> BuildDashboardAsync(TeamAssignmentFormViewModel? formModel = null)
    {
        var userRoles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var usersById = new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in TeamRoles)
        {
            var users = await _userManager.GetUsersInRoleAsync(role);
            foreach (var user in users)
            {
                if (!usersById.ContainsKey(user.Id))
                    usersById[user.Id] = user;

                if (!userRoles.ContainsKey(user.Id))
                    userRoles[user.Id] = role;
            }
        }

        var nodes = usersById.Values
            .Select(u => new TeamMemberNodeViewModel
            {
                UserId = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? "-",
                Role = userRoles.TryGetValue(u.Id, out var role) ? role : "Employee",
                Team = u.OrganizationTeam
            })
            .OrderBy(u => u.FullName)
            .ToList();

        var currentAdmin = await _userManager.GetUserAsync(User);

        var assignmentForm = formModel ?? new TeamAssignmentFormViewModel();
        assignmentForm.MemberOptions = nodes
            .Select(n => new SelectListItem($"{n.FullName} ({n.Role})", n.UserId))
            .ToList();

        return new TeamDashboardViewModel
        {
            CeoName = string.IsNullOrWhiteSpace(currentAdmin?.FullName) ? "Me" : currentAdmin!.FullName,
            CeoEmail = currentAdmin?.Email ?? string.Empty,
            Team1Members = nodes.Where(n => n.Team == OrganizationTeam.Team1).ToList(),
            Team2Members = nodes.Where(n => n.Team == OrganizationTeam.Team2).ToList(),
            UnassignedMembers = nodes.Where(n => n.Team == OrganizationTeam.Unassigned).ToList(),
            AssignmentForm = assignmentForm
        };
    }

    private static string GetTeamLabel(OrganizationTeam team) => team switch
    {
        OrganizationTeam.Team1 => "Team 1",
        OrganizationTeam.Team2 => "Team 2",
        _ => "Unassigned"
    };
}
