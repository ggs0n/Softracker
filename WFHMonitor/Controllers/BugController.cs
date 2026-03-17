using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    private const int FreeBugLimit = 5;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBugService _bugService;
    private readonly IBugFixQueueService _bugFixQueue;
    private readonly OpenClawSettings _openClawSettings;

    public BugController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IBugService bugService,
        IBugFixQueueService bugFixQueue,
        IOptions<OpenClawSettings> openClawSettings)
    {
        _db = db;
        _userManager = userManager;
        _bugService = bugService;
        _bugFixQueue = bugFixQueue;
        _openClawSettings = openClawSettings.Value ?? new OpenClawSettings();
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            var restrictToAssigned = User.IsInRole("Developer") || User.IsInRole("Agent");
            var bugs = await _bugService.GetIndexBugsAsync(restrictToAssigned, userId);
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

            if (User.IsInRole("Developer") || User.IsInRole("Agent"))
            {
                var userId = _userManager.GetUserId(User);
                if (bug.AssignedDeveloperId != userId)
                    return Forbid();
            }

            ViewBag.OpenClawFixAgentOptions = GetOpenClawFixAgentOptions();
            ViewBag.AgentUserOptions = await GetAgentUserOptionsAsync();
            var currentUser = await _userManager.GetUserAsync(User);
            ViewBag.HasProAccess = currentUser is not null && HasActiveProAccess(currentUser);

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
        if (!string.IsNullOrWhiteSpace(userId) && await HasReachedFreeBugLimitAsync(userId))
        {
            TempData["Error"] = $"Free plan allows up to {FreeBugLimit} bug reports. Upgrade to Pro to create more.";
            return RedirectToAction("Index", "Payment");
        }

        var vm = new BugFormViewModel();
        await _bugService.PopulateFormOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> Create(BugFormViewModel model)
    {
        var userId = _userManager.GetUserId(User)!;
        if (await HasReachedFreeBugLimitAsync(userId))
        {
            TempData["Error"] = $"Free plan allows up to {FreeBugLimit} bug reports. Upgrade to Pro to continue.";
            return RedirectToAction("Index", "Payment");
        }

        if (!string.IsNullOrWhiteSpace(model.PullRequestUrl) &&
            !Uri.TryCreate(model.PullRequestUrl.Trim(), UriKind.Absolute, out _))
        {
            ModelState.AddModelError(nameof(model.PullRequestUrl), "PR link must be a valid absolute URL.");
        }

        if (model.AssigneeType == BugAssigneeType.Agent && string.IsNullOrWhiteSpace(model.AssignedAgentId))
            ModelState.AddModelError(nameof(model.AssignedAgentId), "Please select an agent.");

        if (!ModelState.IsValid)
            return await ReturnBugFormWithOptions(model);

        try
        {
            var result = await _bugService.CreateAsync(model, userId);
            if (!result.Succeeded)
                return await ReturnBugFormWithOptions(model, result.Error);

            TempData["Success"] = "Bug created.";
            return RedirectToAction(nameof(Details), new { id = result.BugId });
        }
        catch (Exception ex)
        {
            return await ReturnBugFormWithOptions(model, ex.Message);
        }
    }

    [Authorize(Roles = "Admin,Tester,Developer,Agent")]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _bugService.BuildEditViewModelAsync(id);
        if (vm == null) return NotFound();

        var userId = _userManager.GetUserId(User);
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

        if (!ModelState.IsValid)
            return await ReturnBugFormWithOptions(model);

        try
        {
            var result = await _bugService.UpdateAsync(id, model);
            if (!result.Succeeded)
                return await ReturnBugFormWithOptions(model, result.Error);

            TempData["Success"] = "Bug updated.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            return await ReturnBugFormWithOptions(model, ex.Message);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester,Developer")]
    public async Task<IActionResult> UpdateStatus(int id, BugStatus status)
    {
        var bug = await _bugService.GetByIdAsync(id);
        if (bug == null) return NotFound();

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
        if (currentUser == null || !HasActiveProAccess(currentUser))
        {
            TempData["Error"] = "Fix Bug (OpenClaw) is available for Pro plan only.";
            return RedirectToAction("Index", "Payment");
        }

        var bug = await _db.BugReports
            .Include(b => b.AssignedDeveloper)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bug == null) return NotFound();

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

    private async Task<IActionResult> ReturnBugFormWithOptions(BugFormViewModel model, string? error = null)
    {
        if (!string.IsNullOrWhiteSpace(error))
            TempData["Error"] = error;

        await _bugService.PopulateFormOptionsAsync(model);
        return View(model);
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

    private async Task<bool> HasReachedFreeBugLimitAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || HasActiveProAccess(user))
            return false;

        var currentCount = await _db.BugReports
            .CountAsync(b => b.CreatedById == userId);

        return currentCount >= FreeBugLimit;
    }

    private static bool HasActiveProAccess(ApplicationUser user) =>
        user.SubscriptionPlan == SubscriptionPlan.Pro && user.IsProSubscriptionActive;
}
