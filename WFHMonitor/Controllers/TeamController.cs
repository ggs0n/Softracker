using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin")]
public class TeamController : Controller
{
    private static readonly string[] TeamRoles = ["Employee", "Developer", "Tester", "Agent"];

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public TeamController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager)
    {
        _db = db;
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
            ModelState.AddModelError("AssignmentForm.MemberId", "Admin account cannot be assigned to a delivery team.");

        OrgTeam? selectedTeam = null;
        if (model.TeamId.HasValue)
        {
            selectedTeam = await _db.OrgTeams.FirstOrDefaultAsync(t => t.Id == model.TeamId.Value);
            if (selectedTeam == null)
                ModelState.AddModelError("AssignmentForm.TeamId", "Selected team was not found.");
        }

        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildDashboardAsync(assignmentFormModel: model));

        member!.OrgTeamId = selectedTeam?.Id;
        member.OrganizationTeam = MapLegacyTeam(selectedTeam?.Name);

        var update = await _userManager.UpdateAsync(member);
        if (!update.Succeeded)
        {
            TempData["Error"] = update.Errors.FirstOrDefault()?.Description ?? "Unable to assign team.";
            return View(nameof(Index), await BuildDashboardAsync(assignmentFormModel: model));
        }

        TempData["Success"] = selectedTeam == null
            ? $"{member.FullName} moved to Unassigned."
            : $"{member.FullName} assigned to {selectedTeam.Name}.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignProjectTeam([Bind(Prefix = "ProjectAssignmentForm")] ProjectTeamAssignmentFormViewModel model)
    {
        if (!model.ProjectId.HasValue)
            ModelState.AddModelError("ProjectAssignmentForm.ProjectId", "Please select a project.");

        var project = model.ProjectId.HasValue
            ? await _db.ChangeRequests.FirstOrDefaultAsync(c => c.Id == model.ProjectId.Value)
            : null;
        if (project == null)
            ModelState.AddModelError("ProjectAssignmentForm.ProjectId", "Selected project not found.");

        OrgTeam? selectedTeam = null;
        if (model.TeamId.HasValue)
        {
            selectedTeam = await _db.OrgTeams.FirstOrDefaultAsync(t => t.Id == model.TeamId.Value);
            if (selectedTeam == null)
                ModelState.AddModelError("ProjectAssignmentForm.TeamId", "Selected team was not found.");
        }

        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildDashboardAsync(projectAssignmentFormModel: model));

        project!.OrgTeamId = selectedTeam?.Id;
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = selectedTeam == null
            ? $"Project {project.CrNumber} moved to Unassigned."
            : $"Project {project.CrNumber} assigned to {selectedTeam.Name}.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTeam([Bind(Prefix = "TeamCreateForm")] TeamCreateFormViewModel model)
    {
        model.Name = (model.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError("TeamCreateForm.Name", "Team name is required.");

        var exists = await _db.OrgTeams.AnyAsync(t => t.Name.ToLower() == model.Name.ToLower());
        if (exists)
            ModelState.AddModelError("TeamCreateForm.Name", "Team name already exists.");

        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildDashboardAsync(teamCreateFormModel: model));

        _db.OrgTeams.Add(new OrgTeam
        {
            Name = model.Name,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Team \"{model.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameTeam([Bind(Prefix = "TeamRenameForm")] TeamRenameFormViewModel model)
    {
        model.Name = (model.Name ?? string.Empty).Trim();
        if (model.TeamId <= 0)
            ModelState.AddModelError("TeamRenameForm.TeamId", "Please select a team.");
        if (string.IsNullOrWhiteSpace(model.Name))
            ModelState.AddModelError("TeamRenameForm.Name", "New team name is required.");

        var team = await _db.OrgTeams.FirstOrDefaultAsync(t => t.Id == model.TeamId);
        if (team == null)
            ModelState.AddModelError("TeamRenameForm.TeamId", "Selected team not found.");

        if (team != null)
        {
            var duplicate = await _db.OrgTeams
                .AnyAsync(t => t.Id != team.Id && t.Name.ToLower() == model.Name.ToLower());
            if (duplicate)
                ModelState.AddModelError("TeamRenameForm.Name", "Team name already exists.");
        }

        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildDashboardAsync(teamRenameFormModel: model));

        team!.Name = model.Name;
        team.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Team name updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTeam([Bind(Prefix = "TeamDeleteForm")] TeamDeleteFormViewModel model)
    {
        if (model.TeamId <= 0)
            ModelState.AddModelError("TeamDeleteForm.TeamId", "Please select a team to delete.");

        var team = await _db.OrgTeams.FirstOrDefaultAsync(t => t.Id == model.TeamId);
        if (team == null)
            ModelState.AddModelError("TeamDeleteForm.TeamId", "Selected team not found.");

        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildDashboardAsync(teamDeleteFormModel: model));

        var members = await _db.Users
            .Where(u => u.OrgTeamId == team!.Id)
            .ToListAsync();

        foreach (var member in members)
        {
            member.OrgTeamId = null;
            member.OrganizationTeam = OrganizationTeam.Unassigned;
        }

        var projects = await _db.ChangeRequests
            .Where(c => c.OrgTeamId == team!.Id)
            .ToListAsync();

        foreach (var project in projects)
        {
            project.OrgTeamId = null;
            project.UpdatedAt = DateTime.UtcNow;
        }

        _db.OrgTeams.Remove(team!);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Team \"{team!.Name}\" deleted. {members.Count} member(s) and {projects.Count} project(s) moved to Unassigned.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCeo([Bind(Prefix = "CeoForm")] TeamCeoFormViewModel model)
    {
        model.CeoUserId = (model.CeoUserId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(model.CeoUserId))
            ModelState.AddModelError("CeoForm.CeoUserId", "Please select a CEO account.");

        var ceoUser = string.IsNullOrWhiteSpace(model.CeoUserId)
            ? null
            : await _userManager.FindByIdAsync(model.CeoUserId);
        if (ceoUser == null)
            ModelState.AddModelError("CeoForm.CeoUserId", "Selected CEO account not found.");

        if (!ModelState.IsValid)
            return View(nameof(Index), await BuildDashboardAsync(ceoFormModel: model));

        var profile = await _db.OrganizationProfiles.FirstOrDefaultAsync(p => p.Id == 1);
        if (profile == null)
        {
            profile = new OrganizationProfile
            {
                Id = 1,
                CeoUserId = ceoUser!.Id,
                UpdatedAt = DateTime.UtcNow
            };
            _db.OrganizationProfiles.Add(profile);
        }
        else
        {
            profile.CeoUserId = ceoUser!.Id;
            profile.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"CEO updated to {ceoUser.FullName}.";
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

    private async Task<TeamDashboardViewModel> BuildDashboardAsync(
        TeamAssignmentFormViewModel? assignmentFormModel = null,
        ProjectTeamAssignmentFormViewModel? projectAssignmentFormModel = null,
        TeamCreateFormViewModel? teamCreateFormModel = null,
        TeamRenameFormViewModel? teamRenameFormModel = null,
        TeamDeleteFormViewModel? teamDeleteFormModel = null,
        TeamCeoFormViewModel? ceoFormModel = null)
    {
        var teams = await _db.OrgTeams
            .OrderBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync();

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

        var members = usersById.Values
            .Select(u => new TeamMemberNodeViewModel
            {
                UserId = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? "-",
                Role = userRoles.TryGetValue(u.Id, out var role) ? role : "Employee",
                TeamId = u.OrgTeamId
            })
            .OrderBy(u => u.FullName)
            .ToList();

        var projects = await _db.ChangeRequests
            .OrderByDescending(c => c.UpdatedAt)
            .ThenByDescending(c => c.Id)
            .AsNoTracking()
            .Select(c => new ProjectTeamNodeViewModel
            {
                ProjectId = c.Id,
                CrNumber = c.CrNumber,
                Title = c.Title,
                Stage = c.Stage.ToString(),
                Status = c.Status.ToString(),
                TeamId = c.OrgTeamId
            })
            .ToListAsync();

        var teamCards = teams
            .Select(t => new OrganizationTeamCardViewModel
            {
                TeamId = t.Id,
                TeamName = t.Name,
                Members = members.Where(m => m.TeamId == t.Id).OrderBy(m => m.FullName).ToList(),
                Projects = projects.Where(p => p.TeamId == t.Id).OrderBy(p => p.CrNumber).ToList()
            })
            .ToList();

        var teamOptions = teams
            .Select(t => new SelectListItem(t.Name, t.Id.ToString()))
            .ToList();

        var assignmentForm = assignmentFormModel ?? new TeamAssignmentFormViewModel();
        assignmentForm.MemberOptions = members
            .Select(n => new SelectListItem($"{n.FullName} ({n.Role})", n.UserId))
            .ToList();
        assignmentForm.TeamOptions = BuildTeamOptionsWithUnassigned(teamOptions);

        var projectAssignmentForm = projectAssignmentFormModel ?? new ProjectTeamAssignmentFormViewModel();
        projectAssignmentForm.ProjectOptions = projects
            .Select(p => new SelectListItem($"{p.CrNumber} - {p.Title}", p.ProjectId.ToString()))
            .ToList();
        projectAssignmentForm.TeamOptions = BuildTeamOptionsWithUnassigned(teamOptions);

        var renameForm = teamRenameFormModel ?? new TeamRenameFormViewModel();
        renameForm.TeamOptions = teamOptions;
        var deleteForm = teamDeleteFormModel ?? new TeamDeleteFormViewModel();
        deleteForm.TeamOptions = teamCards
            .Select(t => new SelectListItem(
                $"{t.TeamName} ({t.Members.Count} members, {t.Projects.Count} projects)",
                t.TeamId.ToString()))
            .ToList();

        var allUsers = _userManager.Users
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.Email });

        var profile = await _db.OrganizationProfiles
            .Include(p => p.CeoUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == 1);

        var currentAdmin = await _userManager.GetUserAsync(User);
        var ceoForm = ceoFormModel ?? new TeamCeoFormViewModel();
        ceoForm.CeoUserId = string.IsNullOrWhiteSpace(ceoForm.CeoUserId)
            ? (profile?.CeoUserId ?? string.Empty)
            : ceoForm.CeoUserId;
        ceoForm.CeoOptions = await allUsers
            .Select(u => new SelectListItem(
                string.IsNullOrWhiteSpace(u.Email) ? u.FullName : $"{u.FullName} ({u.Email})",
                u.Id))
            .ToListAsync();

        return new TeamDashboardViewModel
        {
            CeoName = profile?.CeoUser?.FullName
                ?? (string.IsNullOrWhiteSpace(currentAdmin?.FullName) ? "Me" : currentAdmin!.FullName),
            CeoEmail = profile?.CeoUser?.Email ?? (currentAdmin?.Email ?? string.Empty),
            Teams = teamCards,
            UnassignedMembers = members.Where(n => !n.TeamId.HasValue).ToList(),
            UnassignedProjects = projects.Where(p => !p.TeamId.HasValue).ToList(),
            AssignmentForm = assignmentForm,
            ProjectAssignmentForm = projectAssignmentForm,
            TeamCreateForm = teamCreateFormModel ?? new TeamCreateFormViewModel(),
            TeamRenameForm = renameForm,
            TeamDeleteForm = deleteForm,
            CeoForm = ceoForm
        };
    }

    private static List<SelectListItem> BuildTeamOptionsWithUnassigned(IEnumerable<SelectListItem> teamOptions)
    {
        var options = teamOptions
            .Select(t => new SelectListItem(t.Text, t.Value))
            .ToList();
        options.Insert(0, new SelectListItem("Unassigned", string.Empty));
        return options;
    }

    private static OrganizationTeam MapLegacyTeam(string? teamName)
    {
        if (string.Equals(teamName, "Team 1", StringComparison.OrdinalIgnoreCase))
            return OrganizationTeam.Team1;
        if (string.Equals(teamName, "Team 2", StringComparison.OrdinalIgnoreCase))
            return OrganizationTeam.Team2;
        return OrganizationTeam.Unassigned;
    }
}
