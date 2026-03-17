using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin,Tester,Developer,Agent")]
public class BugController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBugService _bugService;
    private readonly IBugFixQueueService _bugFixQueue;
    private readonly OpenClawSettings _openClawSettings;
    private readonly ISystemSettingsService _systemSettingsService;

    public BugController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IBugService bugService,
        IBugFixQueueService bugFixQueue,
        ISystemSettingsService systemSettingsService,
        IOptions<OpenClawSettings> openClawSettings)
    {
        _db = db;
        _userManager = userManager;
        _bugService = bugService;
        _bugFixQueue = bugFixQueue;
        _systemSettingsService = systemSettingsService;
        _openClawSettings = openClawSettings.Value ?? new OpenClawSettings();
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var actionName = context.ActionDescriptor.RouteValues.TryGetValue("action", out var action)
            ? action ?? string.Empty
            : string.Empty;

        var requiresModify = !string.Equals(actionName, nameof(Index), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(actionName, nameof(Details), StringComparison.OrdinalIgnoreCase);

        var allowed = requiresModify
            ? await _systemSettingsService.CanModifyModuleAsync(User, AppModuleKeys.Bugs)
            : await _systemSettingsService.CanViewModuleAsync(User, AppModuleKeys.Bugs);

        if (!allowed)
        {
            context.Result = Forbid();
            return;
        }

        await next();
    }

    public async Task<IActionResult> Index(BugStatus? status)
    {
        try
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var bugs = await _bugService.GetIndexBugsAsync(
                User.IsInRole("Admin"),
                currentUser?.OrgTeamId,
                status);
            ViewBag.SelectedStatus = status;
            return View(bugs);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return View(new List<BugReport>());
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var bug = await _bugService.GetDetailsAsync(id);
            if (bug == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (!CanAccessBugByTeam(bug, currentUser))
                return Forbid();

            ViewBag.OpenClawFixAgentOptions = GetOpenClawFixAgentOptions();
            ViewBag.AgentUserOptions = await GetAgentUserOptionsAsync();
            var planSettings = await _systemSettingsService.GetProVersionSettingsAsync();
            ViewBag.IsOpenClawEnabled = planSettings.EnableOpenClawAgents;
            ViewBag.HasProAccess = currentUser is not null && HasOpenClawAccess(currentUser, planSettings);

            return View(bug);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> Create()
    {
        var userId = _userManager.GetUserId(User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var (isReached, freeLimit) = await HasReachedFreeBugLimitAsync(userId);
            if (isReached)
            {
                TempData["Error"] = $"Free plan allows up to {freeLimit} bug reports. Upgrade to Pro to create more.";
                return RedirectToAction("Index", "Payment");
            }
        }

        var vm = new BugFormViewModel();
        var currentUser = await _userManager.GetUserAsync(User);
        await _bugService.PopulateFormOptionsAsync(vm, User.IsInRole("Admin"), currentUser?.OrgTeamId);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> Create(BugFormViewModel model)
    {
        var userId = _userManager.GetUserId(User)!;
        var (isReached, freeLimit) = await HasReachedFreeBugLimitAsync(userId);
        if (isReached)
        {
            TempData["Error"] = $"Free plan allows up to {freeLimit} bug reports. Upgrade to Pro to continue.";
            return RedirectToAction("Index", "Payment");
        }

        if (!string.IsNullOrWhiteSpace(model.PullRequestUrl) &&
            !Uri.TryCreate(model.PullRequestUrl.Trim(), UriKind.Absolute, out _))
        {
            ModelState.AddModelError(nameof(model.PullRequestUrl), "PR link must be a valid absolute URL.");
        }

        if (model.AssigneeType == BugAssigneeType.Agent && string.IsNullOrWhiteSpace(model.AssignedAgentId))
            ModelState.AddModelError(nameof(model.AssignedAgentId), "Please select an agent.");

        var currentUser = await _userManager.GetUserAsync(User);
        if (model.ChangeRequestId.HasValue &&
            !await CanAccessProjectByTeamAsync(model.ChangeRequestId, currentUser))
        {
            ModelState.AddModelError(nameof(model.ChangeRequestId), "You cannot link this bug to a project outside your team.");
        }

        if (!ModelState.IsValid)
            return await ReturnBugFormWithOptions(model, currentUser);

        try
        {
            var result = await _bugService.CreateAsync(model, userId);
            if (!result.Succeeded)
                return await ReturnBugFormWithOptions(model, currentUser, result.Error);

            TempData["Success"] = "Bug created.";
            return RedirectToAction(nameof(Details), new { id = result.BugId });
        }
        catch (Exception ex)
        {
            return await ReturnBugFormWithOptions(model, currentUser, ex.Message);
        }
    }

    [Authorize(Roles = "Admin,Tester,Developer,Agent")]
    public async Task<IActionResult> Edit(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var bug = await _bugService.GetDetailsAsync(id);
        if (bug == null) return NotFound();
        if (!CanAccessBugByTeam(bug, currentUser))
            return Forbid();

        var vm = await _bugService.BuildEditViewModelAsync(id, User.IsInRole("Admin"), currentUser?.OrgTeamId);
        if (vm == null) return NotFound();

        var userId = currentUser?.Id;
        var isPrivilegedEditor = User.IsInRole("Admin") || User.IsInRole("Tester");
        if (!isPrivilegedEditor && vm.AssignedDeveloperId != userId && vm.AssignedAgentId != userId)
            return Forbid();

        ViewBag.IsPrLinkOnly = !isPrivilegedEditor;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester,Developer,Agent")]
    public async Task<IActionResult> UpdatePullRequestUrl(int id, string? pullRequestUrl)
    {
        var bug = await _bugService.GetByIdAsync(id);
        if (bug == null)
            return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (bug.ChangeRequestId.HasValue && !await CanAccessProjectByTeamAsync(bug.ChangeRequestId, currentUser))
            return Forbid();

        var userId = _userManager.GetUserId(User);
        var isPrivilegedEditor = User.IsInRole("Admin") || User.IsInRole("Tester");
        if (!isPrivilegedEditor && bug.AssignedDeveloperId != userId)
            return Forbid();

        if (!string.IsNullOrWhiteSpace(pullRequestUrl) &&
            !Uri.TryCreate(pullRequestUrl.Trim(), UriKind.Absolute, out _))
        {
            TempData["Error"] = "PR link must be a valid absolute URL.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var result = await _bugService.UpdatePullRequestUrlAsync(id, pullRequestUrl);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? "PR link updated."
            : result.Error;
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> Edit(int id, BugFormViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.PullRequestUrl) &&
            !Uri.TryCreate(model.PullRequestUrl.Trim(), UriKind.Absolute, out _))
        {
            ModelState.AddModelError(nameof(model.PullRequestUrl), "PR link must be a valid absolute URL.");
        }

        if (model.AssigneeType == BugAssigneeType.Agent && string.IsNullOrWhiteSpace(model.AssignedAgentId))
            ModelState.AddModelError(nameof(model.AssignedAgentId), "Please select an agent.");

        var currentUser = await _userManager.GetUserAsync(User);
        if (model.ChangeRequestId.HasValue &&
            !await CanAccessProjectByTeamAsync(model.ChangeRequestId, currentUser))
        {
            ModelState.AddModelError(nameof(model.ChangeRequestId), "You cannot link this bug to a project outside your team.");
        }

        if (!ModelState.IsValid)
            return await ReturnBugFormWithOptions(model, currentUser);

        try
        {
            var result = await _bugService.UpdateAsync(id, model);
            if (!result.Succeeded)
                return await ReturnBugFormWithOptions(model, currentUser, result.Error);

            TempData["Success"] = "Bug updated.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            return await ReturnBugFormWithOptions(model, currentUser, ex.Message);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester,Developer")]
    public async Task<IActionResult> UpdateStatus(int id, BugStatus status)
    {
        var bug = await _bugService.GetByIdAsync(id);
        if (bug == null) return NotFound();

        var currentUser = await _userManager.GetUserAsync(User);
        if (bug.ChangeRequestId.HasValue && !await CanAccessProjectByTeamAsync(bug.ChangeRequestId, currentUser))
            return Forbid();

        var userId = _userManager.GetUserId(User);
        if (!CanUpdateStatus(bug, status, userId))
            return Forbid();

        await _bugService.UpdateStatusAsync(bug, status);

        TempData["Success"] = "Status updated.";
        return RedirectToAction(nameof(Details), new { id = bug.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester,Developer")]
    public async Task<IActionResult> FixWithAgent(int id, string? fixAgentId, string? assignedAgentId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var currentUser = await _userManager.FindByIdAsync(userId);
        var planSettings = await _systemSettingsService.GetProVersionSettingsAsync();
        if (!planSettings.EnableOpenClawAgents)
        {
            TempData["Error"] = "OpenClaw agents are temporarily disabled by admin.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (currentUser == null || !HasOpenClawAccess(currentUser, planSettings))
        {
            TempData["Error"] = "Fix Bug (OpenClaw) is available for Pro plan only.";
            return RedirectToAction("Index", "Payment");
        }

        var bug = await _db.BugReports
            .Include(b => b.AssignedDeveloper)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bug == null) return NotFound();
        if (bug.ChangeRequestId.HasValue && !await CanAccessProjectByTeamAsync(bug.ChangeRequestId, currentUser))
            return Forbid();

        if (User.IsInRole("Developer"))
        {
            if (bug.AssignedDeveloperId != userId)
                return Forbid();
        }

        var configuredFixAgentIds = OpenClawBugScanService.GetConfiguredFixAgentIds(_openClawSettings);
        var selectedFixAgentId = string.IsNullOrWhiteSpace(fixAgentId)
            ? configuredFixAgentIds.FirstOrDefault() ?? "main"
            : fixAgentId.Trim();
        if (configuredFixAgentIds.Count > 0 &&
            !configuredFixAgentIds.Any(a => a.Equals(selectedFixAgentId, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["Error"] = "Selected OpenClaw fix agent is not allowed by configuration.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var assignToAgentId = string.IsNullOrWhiteSpace(assignedAgentId)
            ? await ResolveOpenClawAssigneeAgentIdAsync()
            : assignedAgentId.Trim();
        if (string.IsNullOrWhiteSpace(assignToAgentId))
        {
            TempData["Error"] = "Agent user not found. Create an Agent user first.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var assignedUser = await _userManager.FindByIdAsync(assignToAgentId);
        if (assignedUser == null || !await _userManager.IsInRoleAsync(assignedUser, "Agent"))
        {
            TempData["Error"] = "Selected app agent user is invalid.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var wasAgentProcessing = bug.AssigneeType == BugAssigneeType.Agent &&
                                 bug.AgentStatus is BugAgentStatus.Queued or BugAgentStatus.InProgress;

        var oldStatus = bug.Status;
        var oldAssignedId = bug.AssignedDeveloperId;

        bug.AssigneeType = BugAssigneeType.Agent;
        bug.AgentStatus = BugAgentStatus.Queued;
        bug.AssignedDeveloperId = assignToAgentId;
        bug.UpdatedAt = DateTime.UtcNow;

        _db.BugActivities.Add(new BugActivity
        {
            BugReportId = bug.Id,
            Action = wasAgentProcessing
                ? $"OpenClaw fix manually re-queued ({selectedFixAgentId})"
                : $"OpenClaw fix queued ({selectedFixAgentId})",
            OldStatus = oldStatus,
            NewStatus = bug.Status,
            OldAssignedDeveloperId = oldAssignedId,
            NewAssignedDeveloperId = bug.AssignedDeveloperId
        });

        await _db.SaveChangesAsync();
        await _bugFixQueue.EnqueueAsync(new BugFixQueueItem(
            bug.Id,
            selectedFixAgentId,
            assignToAgentId,
            _userManager.GetUserId(User) ?? string.Empty),
            HttpContext.RequestAborted);

        TempData["Success"] = wasAgentProcessing
            ? $"OpenClaw fix re-queued with '{selectedFixAgentId}'."
            : $"OpenClaw fix queued with '{selectedFixAgentId}'.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> UploadScreenshot(int id, IFormFile file, string? returnUrl)
    {
        return await HandleUploadResult(
            _bugService.UploadScreenshotAsync(id, file),
            "Screenshot uploaded.",
            returnUrl,
            id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> DeleteScreenshot(int screenshotId, int bugId)
    {
        var result = await _bugService.DeleteScreenshotAsync(screenshotId);
        if (!result.Succeeded) return NotFound();
        TempData["Success"] = "Screenshot deleted.";
        return RedirectToAction(nameof(Details), new { id = bugId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> UploadDocument(int id, IFormFile file, string? returnUrl)
    {
        return await HandleUploadResult(
            _bugService.UploadDocumentAsync(id, file),
            "Document uploaded.",
            returnUrl,
            id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> DeleteDocument(int documentId, int bugId)
    {
        var result = await _bugService.DeleteDocumentAsync(documentId);
        if (!result.Succeeded) return NotFound();
        TempData["Success"] = "Document deleted.";
        return RedirectToAction(nameof(Details), new { id = bugId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _bugService.DeleteBugAsync(id);
        if (!result.Succeeded) return NotFound();
        TempData["Success"] = "Bug deleted.";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectToLocal(string? returnUrl, int id)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Details), new { id });
    }

    private bool CanUpdateStatus(BugReport bug, BugStatus status, string? userId)
    {
        var isDeveloper = User.IsInRole("Developer");
        var isAdminOrTester = User.IsInRole("Admin") || User.IsInRole("Tester");

        if (isDeveloper)
        {
            if (bug.AssignedDeveloperId != userId || bug.AssigneeType == BugAssigneeType.Agent)
                return false;
        }

        if (!isAdminOrTester && status is not BugStatus.New and not BugStatus.Testing)
            return false;

        return true;
    }

    private async Task<IActionResult> ReturnBugFormWithOptions(BugFormViewModel model, ApplicationUser? currentUser, string? error = null)
    {
        if (!string.IsNullOrWhiteSpace(error))
            TempData["Error"] = error;

        await _bugService.PopulateFormOptionsAsync(model, User.IsInRole("Admin"), currentUser?.OrgTeamId);
        return View(model);
    }

    private bool CanAccessBugByTeam(BugReport bug, ApplicationUser? currentUser)
    {
        if (User.IsInRole("Admin"))
            return true;

        if (bug.ChangeRequestId.HasValue)
        {
            if (bug.ChangeRequest?.OrgTeamId is null)
                return true;

            return currentUser?.OrgTeamId.HasValue == true && currentUser.OrgTeamId == bug.ChangeRequest.OrgTeamId;
        }

        if (User.IsInRole("Developer") || User.IsInRole("Agent"))
            return bug.AssignedDeveloperId == currentUser?.Id;

        return true;
    }

    private async Task<bool> CanAccessProjectByTeamAsync(int? changeRequestId, ApplicationUser? currentUser)
    {
        if (!changeRequestId.HasValue)
            return true;

        var project = await _db.ChangeRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == changeRequestId.Value);
        if (project == null)
            return false;

        if (User.IsInRole("Admin"))
            return true;

        if (!project.OrgTeamId.HasValue)
            return true;

        return currentUser?.OrgTeamId.HasValue == true && currentUser.OrgTeamId == project.OrgTeamId;
    }

    private async Task<IActionResult> HandleUploadResult(
        Task<(bool Succeeded, string Error)> operation,
        string successMessage,
        string? returnUrl,
        int bugId)
    {
        var result = await operation;
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? successMessage : result.Error;
        return RedirectToLocal(returnUrl, bugId);
    }

    private List<SelectListItem> GetOpenClawFixAgentOptions()
    {
        var configured = OpenClawBugScanService.GetConfiguredFixAgentIds(_openClawSettings);
        return configured
            .Select(id => new SelectListItem(id, id))
            .ToList();
    }

    private async Task<List<SelectListItem>> GetAgentUserOptionsAsync()
    {
        var agents = await _userManager.GetUsersInRoleAsync("Agent");
        return agents
            .OrderBy(a => a.FullName)
            .Select(a => new SelectListItem(a.FullName, a.Id))
            .ToList();
    }

    private async Task<string?> ResolveOpenClawAssigneeAgentIdAsync()
    {
        var agents = await _userManager.GetUsersInRoleAsync("Agent");
        var preferred = agents
            .OrderBy(a => a.FullName)
            .FirstOrDefault(a =>
                (a.FullName?.Contains("openclaw", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.UserName?.Contains("openclaw", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.Email?.Contains("openclaw", StringComparison.OrdinalIgnoreCase) ?? false));

        return preferred?.Id ?? agents.OrderBy(a => a.FullName).FirstOrDefault()?.Id;
    }

    private async Task<(bool IsReached, int FreeLimit)> HasReachedFreeBugLimitAsync(string userId)
    {
        var settings = await _systemSettingsService.GetProVersionSettingsAsync();
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || HasActiveProAccess(user))
            return (false, settings.FreeBugLimit);

        var currentCount = await _db.BugReports
            .CountAsync(b => b.CreatedById == userId);

        return (currentCount >= settings.FreeBugLimit, settings.FreeBugLimit);
    }

    private static bool HasActiveProAccess(ApplicationUser user)
    {
        if (user.SubscriptionPlan != SubscriptionPlan.Pro || !user.IsProSubscriptionActive)
            return false;

        return !user.ProSubscriptionEndsAt.HasValue || user.ProSubscriptionEndsAt.Value > DateTime.UtcNow;
    }

    private static bool HasOpenClawAccess(ApplicationUser user, ProVersionSettingsViewModel settings) =>
        settings.EnableOpenClawAgents &&
        (HasActiveProAccess(user) || settings.AllowOpenClawForFreePlan);
}
