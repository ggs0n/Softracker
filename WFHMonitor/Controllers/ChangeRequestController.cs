using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;
using IWebHostEnvironment = Microsoft.AspNetCore.Hosting.IWebHostEnvironment;

namespace WFHMonitor.Controllers;

[Authorize]
public class ChangeRequestController : Controller
{
    private static readonly HashSet<string> AllProjectsViewActions = [nameof(Index), nameof(Details)];
    private static readonly HashSet<string> FeaturesViewActions = [nameof(Features), nameof(FeatureDetails)];
    private static readonly HashSet<string> FeaturesModifyActions =
    [
        nameof(CreateFeature),
        nameof(EditFeature),
        nameof(DeleteFeature),
        nameof(PickupFeature),
        nameof(AssignFeatureToAgent),
        nameof(UploadFeatureScreenshot),
        nameof(DeleteFeatureScreenshot),
        nameof(AddFeature),
        nameof(ToggleFeature)
    ];

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IGitHubService _gitHub;
    private readonly IProjectBugScanQueueService _projectBugScanQueueService;
    private readonly IFeatureAgentQueueService _featureAgentQueueService;
    private readonly INotificationService _notificationService;
    private readonly OpenClawSettings _openClawSettings;
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly IWebHostEnvironment env;

    public ChangeRequestController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IGitHubService gitHub,
        IProjectBugScanQueueService projectBugScanQueueService,
        IFeatureAgentQueueService featureAgentQueueService,
        INotificationService notificationService,
        ISystemSettingsService systemSettingsService,
        Microsoft.Extensions.Options.IOptions<OpenClawSettings> openClawSettings,
        IWebHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _gitHub = gitHub;
        _projectBugScanQueueService = projectBugScanQueueService;
        _featureAgentQueueService = featureAgentQueueService;
        _notificationService = notificationService;
        _systemSettingsService = systemSettingsService;
        _openClawSettings = openClawSettings.Value ?? new OpenClawSettings();
        this.env = env;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var actionName = context.ActionDescriptor.RouteValues.TryGetValue("action", out var action)
            ? action ?? string.Empty
            : string.Empty;

        var permissionCheck = ResolvePermissionCheck(actionName);
        if (permissionCheck is not null)
        {
            var (moduleKey, requiresModify) = permissionCheck.Value;
            var allowed = requiresModify
                ? await _systemSettingsService.CanModifyModuleAsync(User, moduleKey)
                : await _systemSettingsService.CanViewModuleAsync(User, moduleKey);

            if (!allowed)
            {
                context.Result = Forbid();
                return;
            }
        }

        await next();
    }

    public async Task<IActionResult> Index()
    {
        var query = _db.ChangeRequests
            .Include(c => c.CreatedBy)
            .Include(c => c.Pics).ThenInclude(p => p.Employee)
            .Include(c => c.Features)
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking();

        var crs = await GetVisibleProjectsAsync(query);

        return View(crs);
    }

    public async Task<IActionResult> Features(int? projectId)
    {
        var query = _db.ChangeRequests
            .Include(c => c.Features.OrderBy(f => f.Name))
                .ThenInclude(f => f.AssignedDeveloper)
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking();

        if (projectId.HasValue)
            query = query.Where(c => c.Id == projectId.Value);

        var projects = await GetVisibleProjectsAsync(query);
        if (projectId.HasValue && projects.Count == 0)
            return NotFound();

        return View(projects);
    }

    public async Task<IActionResult> FeatureDetails(int featureId)
    {
        var feature = await _db.ProjectFeatures
            .Include(f => f.ChangeRequest)
                .ThenInclude(c => c!.CreatedBy)
            .Include(f => f.AssignedDeveloper)
            .Include(f => f.Screenshots.OrderByDescending(s => s.UploadedAt))
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == featureId);
        if (feature == null) return NotFound();
        if (!await CanViewProjectAsync(feature.ChangeRequestId))
            return Forbid();

        var currentUser = await _userManager.GetUserAsync(User);
        var planSettings = await _systemSettingsService.GetProVersionSettingsAsync();
        ViewBag.IsOpenClawEnabled = planSettings.EnableOpenClawAgents;
        ViewBag.HasProAccess = currentUser is not null && HasOpenClawAccess(currentUser, planSettings);
        ViewBag.OpenClawFeatureAgentOptions = GetOpenClawFeatureAgentOptions();
        if (User.IsInRole("Admin") || User.IsInRole("Agent"))
            ViewBag.AgentUserOptions = await GetAgentUserOptionsAsync();

        return View(feature);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateFeature(int? projectId, string? returnUrl)
    {
        var userId = _userManager.GetUserId(User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var (isReached, freeLimit) = await HasReachedFreeFeatureLimitAsync(userId);
            if (isReached)
            {
                TempData["Error"] = $"Free plan allows up to {freeLimit} features. Upgrade to Pro to create more.";
                return RedirectToAction("Index", "Payment");
            }
        }

        var vm = new CreateProjectFeatureViewModel
        {
            ChangeRequestId = projectId ?? 0,
            ReturnUrl = returnUrl,
            ProjectOptions = await GetProjectOptionsAsync(),
            DeveloperOptions = await GetDeveloperOptionsAsync()
        };
        EnsureDefaultFeatureTimeline(vm);

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateFeature(CreateProjectFeatureViewModel model)
    {
        EnsureDefaultFeatureTimeline(model);

        var userId = _userManager.GetUserId(User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var (isReached, freeLimit) = await HasReachedFreeFeatureLimitAsync(userId);
            if (isReached)
            {
                TempData["Error"] = $"Free plan allows up to {freeLimit} features. Upgrade to Pro to continue.";
                return RedirectToAction("Index", "Payment");
            }
        }

        if (!ModelState.IsValid)
        {
            model.ProjectOptions = await GetEditableProjectOptionsAsync();
            model.DeveloperOptions = await GetDeveloperOptionsAsync();
            return View(model);
        }

        var projectExists = await _db.ChangeRequests
            .AnyAsync(c => c.Id == model.ChangeRequestId);

        if (!projectExists)
        {
            ModelState.AddModelError(nameof(model.ChangeRequestId), "Selected project not found.");
            model.ProjectOptions = await GetProjectOptionsAsync();
            model.DeveloperOptions = await GetDeveloperOptionsAsync();
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.AssignedDeveloperId))
        {
            var assignedUser = await _userManager.FindByIdAsync(model.AssignedDeveloperId);
            if (assignedUser == null || !await IsFeatureAssigneeValidAsync(assignedUser))
            {
                ModelState.AddModelError(nameof(model.AssignedDeveloperId), "Selected assignee must be a Developer or Agent.");
                model.ProjectOptions = await GetEditableProjectOptionsAsync();
                model.DeveloperOptions = await GetDeveloperOptionsAsync();
                return View(model);
            }
        }

        var normalizedName = model.Name.Trim();
        var existsWithSameName = await _db.ProjectFeatures
            .AnyAsync(f => f.ChangeRequestId == model.ChangeRequestId
                && f.Name.ToLower() == normalizedName.ToLower());

        if (existsWithSameName)
        {
            ModelState.AddModelError(nameof(model.Name), "This feature already exists for the selected project.");
            model.ProjectOptions = await GetProjectOptionsAsync();
            model.DeveloperOptions = await GetDeveloperOptionsAsync();
            return View(model);
        }

        var isCompleted = model.Status == CrStatus.Done;
        var featureNumber = await GenerateNextFeatureNumberAsync(DateTime.UtcNow.Year);

        var feature = new ProjectFeature
        {
            ChangeRequestId = model.ChangeRequestId,
            FeatureNumber = featureNumber,
            Name = normalizedName,
            Description = model.Description?.Trim(),
            ModuleImpacted = model.ModuleImpacted?.Trim(),
            LinkedBugs = model.LinkedBugs?.Trim(),
            PullRequestUrl = model.PullRequestUrl?.Trim(),
            Status = model.Status,
            Priority = model.Priority,
            Stage = model.Stage,
            TimelineStart = model.TimelineStart,
            TimelineEnd = model.TimelineEnd,
            AssignedDeveloperId = string.IsNullOrWhiteSpace(model.AssignedDeveloperId)
                ? null
                : model.AssignedDeveloperId.Trim(),
            IsCompleted = isCompleted,
            IsAutoDetected = false
        };
        _db.ProjectFeatures.Add(feature);

        await _db.SaveChangesAsync();
        await NotifyDeveloperFeatureAssignmentAsync(feature);
        TempData["Success"] = $"Feature \"{normalizedName}\" created.";

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return RedirectToAction(nameof(Features), new { projectId = model.ChangeRequestId });
    }

    [Authorize(Roles = "Admin,Developer")]
    public async Task<IActionResult> EditFeature(int featureId, string? returnUrl)
    {
        var feature = await _db.ProjectFeatures
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == featureId);
        if (feature == null) return NotFound();
        if (!await CanEditFeatureProjectAsync(feature.ChangeRequestId)) return Forbid();

        var vm = new CreateProjectFeatureViewModel
        {
            FeatureId = feature.Id,
            ChangeRequestId = feature.ChangeRequestId,
            Name = feature.Name,
            Description = feature.Description,
            ModuleImpacted = feature.ModuleImpacted,
            LinkedBugs = feature.LinkedBugs,
            PullRequestUrl = feature.PullRequestUrl,
            Status = feature.Status,
            Priority = feature.Priority,
            Stage = feature.Stage,
            TimelineStart = feature.TimelineStart,
            TimelineEnd = feature.TimelineEnd,
            AssignedDeveloperId = feature.AssignedDeveloperId,
            ReturnUrl = returnUrl,
            ProjectOptions = await GetEditableProjectOptionsAsync(),
            DeveloperOptions = await GetDeveloperOptionsAsync()
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Developer")]
    public async Task<IActionResult> EditFeature(CreateProjectFeatureViewModel model)
    {
        if (model.FeatureId is null or <= 0)
            return NotFound();

        if (!ModelState.IsValid)
        {
            model.ProjectOptions = await GetProjectOptionsAsync();
            model.DeveloperOptions = await GetDeveloperOptionsAsync();
            return View(model);
        }

        var feature = await _db.ProjectFeatures.FindAsync(model.FeatureId.Value);
        if (feature == null) return NotFound();
        if (!await CanEditFeatureProjectAsync(feature.ChangeRequestId)) return Forbid();

        var projectExists = await CanEditFeatureProjectAsync(model.ChangeRequestId);
        if (!projectExists)
        {
            ModelState.AddModelError(nameof(model.ChangeRequestId), "Selected project not found.");
            model.ProjectOptions = await GetEditableProjectOptionsAsync();
            model.DeveloperOptions = await GetDeveloperOptionsAsync();
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.AssignedDeveloperId))
        {
            var assignedUser = await _userManager.FindByIdAsync(model.AssignedDeveloperId);
            if (assignedUser == null || !await IsFeatureAssigneeValidAsync(assignedUser))
            {
                ModelState.AddModelError(nameof(model.AssignedDeveloperId), "Selected assignee must be a Developer or Agent.");
                model.ProjectOptions = await GetProjectOptionsAsync();
                model.DeveloperOptions = await GetDeveloperOptionsAsync();
                return View(model);
            }
        }

        var normalizedName = model.Name.Trim();
        var existsWithSameName = await _db.ProjectFeatures
            .AnyAsync(f => f.Id != model.FeatureId.Value
                && f.ChangeRequestId == model.ChangeRequestId
                && f.Name.ToLower() == normalizedName.ToLower());
        if (existsWithSameName)
        {
            ModelState.AddModelError(nameof(model.Name), "This feature already exists for the selected project.");
            model.ProjectOptions = await GetEditableProjectOptionsAsync();
            model.DeveloperOptions = await GetDeveloperOptionsAsync();
            return View(model);
        }

        var oldAssignedDeveloperId = feature.AssignedDeveloperId;

        feature.ChangeRequestId = model.ChangeRequestId;
        feature.Name = normalizedName;
        feature.Description = model.Description?.Trim();
        feature.ModuleImpacted = model.ModuleImpacted?.Trim();
        feature.LinkedBugs = model.LinkedBugs?.Trim();
        feature.PullRequestUrl = model.PullRequestUrl?.Trim();
        feature.Status = model.Status;
        feature.Priority = model.Priority;
        feature.Stage = model.Stage;
        feature.TimelineStart = model.TimelineStart;
        feature.TimelineEnd = model.TimelineEnd;
        feature.AssignedDeveloperId = string.IsNullOrWhiteSpace(model.AssignedDeveloperId)
            ? null
            : model.AssignedDeveloperId.Trim();
        feature.IsCompleted = model.Status == CrStatus.Done;

        await _db.SaveChangesAsync();
        await NotifyDeveloperFeatureAssignmentAsync(feature, oldAssignedDeveloperId);
        TempData["Success"] = $"Feature \"{feature.Name}\" updated.";

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return RedirectToAction(nameof(Features), new { projectId = model.ChangeRequestId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Agent")]
    public async Task<IActionResult> PickupFeature(int featureId, string? featureAgentId, string? assignedAgentId, string? returnUrl)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var currentUser = await _userManager.FindByIdAsync(userId);
        var planSettings = await _systemSettingsService.GetProVersionSettingsAsync();
        if (!planSettings.EnableOpenClawAgents)
        {
            TempData["Error"] = "OpenClaw agents are temporarily disabled by admin.";
            return RedirectFeatureLocal(returnUrl, featureId);
        }

        if (currentUser == null || !HasOpenClawAccess(currentUser, planSettings))
        {
            TempData["Error"] = "Feature Agent (OpenClaw) is available for Pro plan only.";
            return RedirectToAction("Index", "Payment");
        }

        var feature = await _db.ProjectFeatures
            .FirstOrDefaultAsync(f => f.Id == featureId);
        if (feature == null) return NotFound();
        if (!await CanViewProjectAsync(feature.ChangeRequestId))
            return Forbid();

        var configuredFeatureAgentIds = OpenClawBugScanService.GetConfiguredFeatureAgentIds(_openClawSettings);
        var selectedFeatureAgentId = string.IsNullOrWhiteSpace(featureAgentId)
            ? configuredFeatureAgentIds.FirstOrDefault() ?? "main"
            : featureAgentId.Trim();
        if (configuredFeatureAgentIds.Count > 0 &&
            !configuredFeatureAgentIds.Any(a => a.Equals(selectedFeatureAgentId, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["Error"] = "Selected OpenClaw feature agent is not allowed by configuration.";
            return RedirectFeatureLocal(returnUrl, featureId);
        }

        var assignToAgentId = string.IsNullOrWhiteSpace(assignedAgentId)
            ? userId
            : assignedAgentId.Trim();
        if (string.IsNullOrWhiteSpace(assignToAgentId))
        {
            TempData["Error"] = "Agent user not found. Create an Agent user first.";
            return RedirectFeatureLocal(returnUrl, featureId);
        }

        var assignedUser = await _userManager.FindByIdAsync(assignToAgentId);
        if (assignedUser == null || !await _userManager.IsInRoleAsync(assignedUser, "Agent"))
        {
            TempData["Error"] = "Selected app agent user is invalid.";
            return RedirectFeatureLocal(returnUrl, featureId);
        }

        if (User.IsInRole("Agent") && !string.Equals(assignToAgentId, userId, StringComparison.Ordinal))
            return Forbid();

        if (User.IsInRole("Agent"))
        {
            if (!string.IsNullOrWhiteSpace(feature.AssignedDeveloperId) &&
                !string.Equals(feature.AssignedDeveloperId, userId, StringComparison.Ordinal))
            {
                TempData["Error"] = "This feature is already assigned to another user.";
                return RedirectFeatureLocal(returnUrl, featureId);
            }
        }

        var wasAgentProcessing = feature.AgentStatus is FeatureAgentStatus.Queued or FeatureAgentStatus.InProgress;
        feature.AssignedDeveloperId = assignToAgentId;
        feature.AgentStatus = FeatureAgentStatus.Queued;
        feature.AgentLastRunAt = DateTime.UtcNow;
        if (feature.Status == CrStatus.Draft)
            feature.Status = CrStatus.InProgress;
        if (feature.Stage == CrStage.ProjectStart || feature.Stage == CrStage.DeploymentComplete)
            feature.Stage = CrStage.Development;

        await _db.SaveChangesAsync();
        await _featureAgentQueueService.EnqueueAsync(new FeatureAgentQueueItem(
            feature.Id,
            selectedFeatureAgentId,
            assignToAgentId,
            userId),
            HttpContext.RequestAborted);

        TempData["Success"] = wasAgentProcessing
            ? $"OpenClaw feature run re-queued with '{selectedFeatureAgentId}'."
            : $"OpenClaw feature run queued with '{selectedFeatureAgentId}'.";
        return RedirectFeatureLocal(returnUrl, featureId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignFeatureToAgent(int featureId, string assignedAgentId, string? featureAgentId, string? returnUrl)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var currentUser = await _userManager.FindByIdAsync(userId);
        var planSettings = await _systemSettingsService.GetProVersionSettingsAsync();
        if (!planSettings.EnableOpenClawAgents)
        {
            TempData["Error"] = "OpenClaw agents are temporarily disabled by admin.";
            return RedirectFeatureLocal(returnUrl, featureId);
        }

        if (currentUser == null || !HasOpenClawAccess(currentUser, planSettings))
        {
            TempData["Error"] = "Feature Agent (OpenClaw) is available for Pro plan only.";
            return RedirectToAction("Index", "Payment");
        }

        assignedAgentId = (assignedAgentId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(assignedAgentId))
        {
            TempData["Error"] = "Please select an agent.";
            return RedirectToAction(nameof(FeatureDetails), new { featureId });
        }

        var feature = await _db.ProjectFeatures
            .FirstOrDefaultAsync(f => f.Id == featureId);
        if (feature == null) return NotFound();
        if (!await CanEditFeatureProjectAsync(feature.ChangeRequestId))
            return Forbid();

        var configuredFeatureAgentIds = OpenClawBugScanService.GetConfiguredFeatureAgentIds(_openClawSettings);
        var selectedFeatureAgentId = string.IsNullOrWhiteSpace(featureAgentId)
            ? configuredFeatureAgentIds.FirstOrDefault() ?? "main"
            : featureAgentId.Trim();
        if (configuredFeatureAgentIds.Count > 0 &&
            !configuredFeatureAgentIds.Any(a => a.Equals(selectedFeatureAgentId, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["Error"] = "Selected OpenClaw feature agent is not allowed by configuration.";
            return RedirectFeatureLocal(returnUrl, featureId);
        }

        var agentUser = await _userManager.FindByIdAsync(assignedAgentId);
        if (agentUser == null || !await _userManager.IsInRoleAsync(agentUser, "Agent"))
        {
            TempData["Error"] = "Selected agent user is invalid.";
            return RedirectFeatureLocal(returnUrl, featureId);
        }

        var wasAgentProcessing = feature.AgentStatus is FeatureAgentStatus.Queued or FeatureAgentStatus.InProgress;
        feature.AssignedDeveloperId = agentUser.Id;
        feature.AgentStatus = FeatureAgentStatus.Queued;
        feature.AgentLastRunAt = DateTime.UtcNow;
        if (feature.Status == CrStatus.Draft)
            feature.Status = CrStatus.InProgress;
        if (feature.Stage == CrStage.ProjectStart || feature.Stage == CrStage.DeploymentComplete)
            feature.Stage = CrStage.Development;
        await _db.SaveChangesAsync();
        await _featureAgentQueueService.EnqueueAsync(new FeatureAgentQueueItem(
            feature.Id,
            selectedFeatureAgentId,
            agentUser.Id,
            userId),
            HttpContext.RequestAborted);

        TempData["Success"] = wasAgentProcessing
            ? $"Feature re-queued for agent {agentUser.FullName} with '{selectedFeatureAgentId}'."
            : $"Feature assigned to agent {agentUser.FullName} and queued with '{selectedFeatureAgentId}'.";
        return RedirectFeatureLocal(returnUrl, featureId);
    }

    public async Task<IActionResult> Details(int id)
    {
        var projectForSync = await _db.ChangeRequests
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (projectForSync == null) return NotFound();
        if (!await CanViewProjectAsync(projectForSync.Id))
            return Forbid();

        if (TryResolveGitHubConfig(projectForSync, out var syncOwner, out var syncRepo, out var syncBranch))
        {
            try
            {
                await RefreshProjectFromGitHubAsync(projectForSync, syncOwner, syncRepo, syncBranch);
            }
            catch (Exception ex)
            {
                ViewBag.SyncWarning = ex.Message;
            }
        }

        var cr = await _db.ChangeRequests
            .Include(c => c.CreatedBy)
            .Include(c => c.Pics).ThenInclude(p => p.Employee)
            .Include(c => c.ArchSpecImages.OrderBy(i => i.SortOrder))
            .Include(c => c.Documents.OrderBy(d => d.UploadedAt))
            .Include(c => c.Features.OrderBy(f => f.Name))
                .ThenInclude(f => f.AssignedDeveloper)
            .Include(c => c.RepositoryFeatures.OrderBy(f => f.Name))
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cr == null) return NotFound();

        ViewBag.LinkedBugs = await _db.BugReports
            .Include(b => b.AssignedDeveloper)
            .Where(b => b.ChangeRequestId == id)
            .OrderByDescending(b => b.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var owner = cr.GitHubRepoOwner;
        var repo = cr.GitHubRepoName;
        var branch = cr.GitHubBranch;
        if ((string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo)) &&
            !string.IsNullOrWhiteSpace(cr.GitHubRepoUrl) &&
            TryParseGitHubRepoUrl(cr.GitHubRepoUrl, out var parsedOwner, out var parsedRepo, out var parsedBranch))
        {
            owner = parsedOwner;
            repo = parsedRepo;
            if (string.IsNullOrWhiteSpace(branch))
                branch = parsedBranch;
        }

        ViewBag.ResolvedGitHubOwner = owner;
        ViewBag.ResolvedGitHubRepo = repo;
        ViewBag.ResolvedGitHubBranch = branch;
        ViewBag.OpenClawScanAgentOptions = GetOpenClawScanAgentOptions();
        var currentUser = await _userManager.GetUserAsync(User);
        var planSettings = await _systemSettingsService.GetProVersionSettingsAsync();
        ViewBag.IsOpenClawEnabled = planSettings.EnableOpenClawAgents;
        ViewBag.HasProAccess = currentUser is not null && HasOpenClawAccess(currentUser, planSettings);

        if (!string.IsNullOrWhiteSpace(owner) &&
            !string.IsNullOrWhiteSpace(repo) &&
            !string.IsNullOrWhiteSpace(branch))
        {
            try
            {
                ViewBag.Commits = await _gitHub.GetCommitsAsync(owner, repo, branch);
            }
            catch (Exception ex)
            {
                ViewBag.CommitError = ex.Message;
            }
        }

        return View(cr);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        var userId = _userManager.GetUserId(User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var (isReached, freeLimit) = await HasReachedFreeProjectLimitAsync(userId);
            if (isReached)
            {
                TempData["Error"] = $"Free plan allows up to {freeLimit} projects. Upgrade to Pro to create more.";
                return RedirectToAction("Index", "Payment");
            }
        }

        var vm = new ChangeRequestFormViewModel
        {
            EmployeeOptions = await GetEmployeeOptions()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(ChangeRequestFormViewModel model)
    {
        ApplyGitHubRepoFromUrl(model);
        var userId = _userManager.GetUserId(User)!;

        var (isReached, freeLimit) = await HasReachedFreeProjectLimitAsync(userId);
        if (isReached)
        {
            TempData["Error"] = $"Free plan allows up to {freeLimit} projects. Upgrade to Pro to continue.";
            return RedirectToAction("Index", "Payment");
        }

        if (!ModelState.IsValid)
        {
            model.EmployeeOptions = await GetEmployeeOptions();
            return View(model);
        }

        var currentYear = DateTime.UtcNow.Year;
        ChangeRequest? cr = null;
        const int maxCrNumberAttempts = 6;

        for (var attempt = 1; attempt <= maxCrNumberAttempts; attempt++)
        {
            var crNumberCandidate = await GenerateNextProjectNumberAsync(currentYear);
            var candidate = BuildChangeRequestEntity(model, userId, crNumberCandidate);

            _db.ChangeRequests.Add(candidate);
            try
            {
                await _db.SaveChangesAsync();
                cr = candidate;
                break;
            }
            catch (DbUpdateException ex) when (IsDuplicateCrNumberException(ex))
            {
                _db.Entry(candidate).State = EntityState.Detached;
                if (attempt == maxCrNumberAttempts)
                    throw;
            }
        }

        if (cr == null)
            throw new InvalidOperationException("Unable to create project number. Please retry.");

        var selectedPics = model.Pics
            .Where(p => !string.IsNullOrWhiteSpace(p.EmployeeId))
            .ToList();

        foreach (var pic in selectedPics)
        {
            _db.ChangeRequestPics.Add(new ChangeRequestPic
            {
                ChangeRequestId = cr.Id,
                EmployeeId = pic.EmployeeId,
                Role = pic.Role
            });
        }

        var seenFeatureNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var feature in model.ImportedFeatures)
        {
            var featureName = feature.Name?.Trim();
            if (string.IsNullOrWhiteSpace(featureName) || !seenFeatureNames.Add(featureName))
                continue;

            _db.RepositoryFeatures.Add(new RepositoryFeature
            {
                ChangeRequestId = cr.Id,
                Name = featureName,
                Description = feature.Description?.Trim(),
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        await NotifyDeveloperProjectAssignmentsAsync(cr, selectedPics);

        TempData["Success"] = $"Project {cr.CrNumber} created.";
        return RedirectToAction(nameof(Details), new { id = cr.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ImportGitHubRepo(string? repoUrl, string? branch)
    {
        repoUrl = repoUrl?.Trim();
        branch = branch?.Trim();

        if (string.IsNullOrWhiteSpace(repoUrl))
            return BadRequest(new { message = "GitHub repository URL is required." });

        if (!TryParseGitHubRepoUrl(repoUrl, out var owner, out var repo, out var branchFromUrl))
            return BadRequest(new { message = "Invalid GitHub repository URL. Example: https://github.com/owner/repo" });

        try
        {
            var repoInfo = await _gitHub.GetRepositoryInfoAsync(owner, repo);

            var resolvedBranch = !string.IsNullOrWhiteSpace(branch)
                ? branch
                : !string.IsNullOrWhiteSpace(branchFromUrl)
                    ? branchFromUrl
                    : repoInfo.DefaultBranch;

            var tree = await _gitHub.GetRepoTreeAsync(owner, repo, resolvedBranch);
            var detectedFeatures = FeatureDetector.DetectFeatures(tree);
            var languages = await _gitHub.GetRepositoryLanguagesAsync(owner, repo);

            var readme = await _gitHub.GetReadmeContentAsync(owner, repo);
            var figmaLink = FindFirstMatchingUrl(repoInfo.Homepage, readme, "figma.com");
            var archSpecLink = FindFirstMatchingUrl(repoInfo.Homepage, readme, "docs.google.com", "confluence", "notion.so", "miro.com");
            var technologyStack = BuildTechnologyStack(languages);

            var importedTitle = HumanizeRepoName(repoInfo.Name);
            var readmeDescription = ExtractReadmeDescription(readme);
            var importedDescription = !string.IsNullOrWhiteSpace(readmeDescription)
                ? readmeDescription
                : BuildRepoPurposeSummary(repoInfo.Name, repoInfo.Description, detectedFeatures, technologyStack);

            return Json(new
            {
                title = importedTitle,
                description = importedDescription,
                gitHubRepoOwner = owner,
                gitHubRepoName = repo,
                gitHubRepoUrl = repoInfo.HtmlUrl,
                gitHubBranch = resolvedBranch,
                technologyStack,
                timelineStart = DateTime.Today.ToString("yyyy-MM-dd"),
                timelineEnd = DateTime.Today.AddMonths(1).ToString("yyyy-MM-dd"),
                figmaLink,
                archSpecLink,
                features = detectedFeatures.Select(f => new
                {
                    name = f.Name,
                    description = f.Description,
                    isAutoDetected = true
                })
            });
        }
        catch (Exception ex)
        {
            var safeMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Unable to import this repository right now."
                : ex.Message;
            return BadRequest(new { message = safeMessage });
        }
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetGitHubRepositories()
    {
        try
        {
            var repos = await _gitHub.GetCurrentUserRepositoriesAsync(300);
            return Json(new
            {
                repositories = repos.Select(r => new
                {
                    name = r.Name,
                    fullName = r.FullName,
                    ownerLogin = r.OwnerLogin,
                    htmlUrl = r.HtmlUrl,
                    defaultBranch = r.DefaultBranch,
                    isPrivate = r.IsPrivate,
                    description = r.Description
                })
            });
        }
        catch (Exception ex)
        {
            var safeMessage = string.IsNullOrWhiteSpace(ex.Message)
                ? "Unable to load repositories from GitHub right now."
                : ex.Message;
            return BadRequest(new { message = safeMessage });
        }
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var cr = await _db.ChangeRequests
            .Include(c => c.Pics)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cr == null) return NotFound();

        var vm = new ChangeRequestFormViewModel
        {
            Id = cr.Id,
            Title = cr.Title,
            Description = cr.Description,
            Status = cr.Status,
            Priority = cr.Priority,
            Stage = cr.Stage,
            FigmaLink = cr.FigmaLink,
            ArchSpecLink = cr.ArchSpecLink,
            ArchSpecNotes = cr.ArchSpecNotes,
            TimelineStart = cr.TimelineStart,
            TimelineEnd = cr.TimelineEnd,
            GitHubRepoOwner = cr.GitHubRepoOwner,
            GitHubRepoName = cr.GitHubRepoName,
            GitHubRepoUrl = cr.GitHubRepoUrl,
            GitHubBranch = cr.GitHubBranch,
            TechnologyStack = cr.TechnologyStack,
            Pics = cr.Pics.Select(p => new PicEntry { EmployeeId = p.EmployeeId, Role = p.Role }).ToList(),
            EmployeeOptions = await GetEmployeeOptions()
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, ChangeRequestFormViewModel model)
    {
        ApplyGitHubRepoFromUrl(model);

        if (!ModelState.IsValid)
        {
            model.EmployeeOptions = await GetEmployeeOptions();
            return View(model);
        }

        var cr = await _db.ChangeRequests
            .Include(c => c.Pics)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cr == null) return NotFound();

        var oldPicEmployeeIds = cr.Pics
            .Where(p => !string.IsNullOrWhiteSpace(p.EmployeeId))
            .Select(p => p.EmployeeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        cr.Title = model.Title;
        cr.Description = model.Description;
        cr.Status = model.Status;
        cr.Priority = model.Priority;
        cr.Stage = model.Stage;
        cr.FigmaLink = model.FigmaLink;
        cr.ArchSpecLink = model.ArchSpecLink;
        cr.ArchSpecNotes = model.ArchSpecNotes;
        cr.TimelineStart = model.TimelineStart;
        cr.TimelineEnd = model.TimelineEnd;
        cr.GitHubRepoOwner = model.GitHubRepoOwner;
        cr.GitHubRepoName = model.GitHubRepoName;
        cr.GitHubRepoUrl = model.GitHubRepoUrl;
        cr.GitHubBranch = model.GitHubBranch;
        cr.TechnologyStack = model.TechnologyStack;
        cr.UpdatedAt = DateTime.UtcNow;

        _db.ChangeRequestPics.RemoveRange(cr.Pics);
        var selectedPics = model.Pics
            .Where(p => !string.IsNullOrWhiteSpace(p.EmployeeId))
            .ToList();

        foreach (var pic in selectedPics)
        {
            _db.ChangeRequestPics.Add(new ChangeRequestPic
            {
                ChangeRequestId = cr.Id,
                EmployeeId = pic.EmployeeId,
                Role = pic.Role
            });
        }

        await _db.SaveChangesAsync();
        await NotifyDeveloperProjectAssignmentsAsync(cr, selectedPics, oldPicEmployeeIds);
        TempData["Success"] = "Project updated.";
        return RedirectToAction(nameof(Details), new { id = cr.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var cr = await _db.ChangeRequests
            .Include(c => c.ArchSpecImages)
            .Include(c => c.Documents)
            .Include(c => c.Features)
                .ThenInclude(f => f.Screenshots)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cr == null) return NotFound();

        // Delete uploaded image files
        foreach (var img in cr.ArchSpecImages)
            DeleteImageFile(img.FileName, env);
        foreach (var doc in cr.Documents)
            DeleteDocumentFile(doc.FileName, env);
        foreach (var feature in cr.Features)
        {
            foreach (var shot in feature.Screenshots)
                DeleteFeatureScreenshotFile(shot.FileName, env);
        }

        var linkedTestCases = await _db.TestCases
            .Where(t => t.ChangeRequestId == id)
            .ToListAsync();
        if (linkedTestCases.Count != 0)
            _db.TestCases.RemoveRange(linkedTestCases);

        _db.ChangeRequests.Remove(cr);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Project deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadImage(int id, IFormFile file, string? caption, string? returnUrl)
    {
        var cr = await _db.ChangeRequests.FindAsync(id);
        if (cr == null) return NotFound();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select an image file.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Details), new { id });
        }

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
        {
            TempData["Error"] = "Only image files (jpg, png, gif, webp) are allowed.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Details), new { id });
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            TempData["Error"] = "Image must be under 10 MB.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Details), new { id });
        }

        var fileName = $"{id}_{Guid.NewGuid():N}{ext}";
        var uploadPath = Path.Combine(env.WebRootPath, "uploads", "archspec", fileName);

        using (var stream = System.IO.File.Create(uploadPath))
            await file.CopyToAsync(stream);

        _db.ArchSpecImages.Add(new ArchSpecImage
        {
            ChangeRequestId = id,
            FileName = fileName,
            Caption = caption?.Trim(),
            SortOrder = await _db.ArchSpecImages.CountAsync(i => i.ChangeRequestId == id)
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = "Image uploaded.";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteImage(int imageId, int crId)
    {
        var img = await _db.ArchSpecImages.FindAsync(imageId);
        if (img == null) return NotFound();

        DeleteImageFile(img.FileName, env);
        _db.ArchSpecImages.Remove(img);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Image deleted.";
        return RedirectToAction(nameof(Details), new { id = crId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadDocument(int id, IFormFile file, string? returnUrl)
    {
        var cr = await _db.ChangeRequests.FindAsync(id);
        if (cr == null) return NotFound();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a document file.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Details), new { id });
        }

        var allowed = new[] { ".pdf", ".docx", ".xlsx", ".xls", ".pptx", ".txt" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
        {
            TempData["Error"] = "Only PDF, Word, Excel, PowerPoint, or TXT files are allowed.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Details), new { id });
        }

        if (file.Length > 20 * 1024 * 1024)
        {
            TempData["Error"] = "Document must be under 20 MB.";
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);
            return RedirectToAction(nameof(Details), new { id });
        }

        var fileName = $"{id}_{Guid.NewGuid():N}{ext}";
        var uploadDir = Path.Combine(env.WebRootPath, "uploads", "docs");
        Directory.CreateDirectory(uploadDir);
        var uploadPath = Path.Combine(uploadDir, fileName);

        using (var stream = System.IO.File.Create(uploadPath))
            await file.CopyToAsync(stream);

        _db.ChangeRequestDocuments.Add(new ChangeRequestDocument
        {
            ChangeRequestId = id,
            FileName = fileName,
            OriginalFileName = Path.GetFileName(file.FileName)
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = "Document uploaded.";
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDocument(int documentId, int crId)
    {
        var doc = await _db.ChangeRequestDocuments.FindAsync(documentId);
        if (doc == null) return NotFound();

        DeleteDocumentFile(doc.FileName, env);
        _db.ChangeRequestDocuments.Remove(doc);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Document deleted.";
        return RedirectToAction(nameof(Details), new { id = crId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ScanFeatures(int id)
    {
        var cr = await _db.ChangeRequests.FindAsync(id);
        if (cr == null) return NotFound();

        var owner = cr.GitHubRepoOwner;
        var repo = cr.GitHubRepoName;
        var branch = cr.GitHubBranch;

        if ((string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo)) &&
            !string.IsNullOrWhiteSpace(cr.GitHubRepoUrl) &&
            TryParseGitHubRepoUrl(cr.GitHubRepoUrl, out var parsedOwner, out var parsedRepo, out var parsedBranch))
        {
            owner = parsedOwner;
            repo = parsedRepo;
            if (string.IsNullOrWhiteSpace(branch))
                branch = parsedBranch;
        }

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
        {
            TempData["Error"] = "No GitHub repository linked to this project.";
            return RedirectToAction(nameof(Details), new { id });
        }

        branch ??= "main";

        try
        {
            var tree = await _gitHub.GetRepoTreeAsync(owner, repo, branch);
            var detected = FeatureDetector.DetectFeatures(tree);

            var existingNames = await _db.RepositoryFeatures
                .Where(f => f.ChangeRequestId == id)
                .Select(f => f.Name)
                .ToListAsync();
            var knownNames = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var added = 0;
            foreach (var (name, description) in detected)
            {
                if (!knownNames.Add(name))
                    continue;

                _db.RepositoryFeatures.Add(new RepositoryFeature
                {
                    ChangeRequestId = id,
                    Name = name,
                    Description = description,
                    UpdatedAt = DateTime.UtcNow
                });
                added++;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = added > 0
                ? $"Scan complete - {added} repository feature(s) detected and added."
                : "Scan complete - no new repository features detected.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Scan failed: {ex.Message}";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester,Developer")]
    public async Task<IActionResult> FindBugs(int id, string? scanAgentId)
    {
        var project = await _db.ChangeRequests
            .FirstOrDefaultAsync(c => c.Id == id);
        if (project == null) return NotFound();
        if (!await CanViewProjectAsync(project.Id))
            return Forbid();

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
            TempData["Error"] = "Find Bugs (OpenClaw) is available for Pro plan only.";
            return RedirectToAction("Index", "Payment");
        }

        var configuredScanAgentIds = OpenClawBugScanService.GetConfiguredAgentIds(_openClawSettings);
        var selectedScanAgentId = string.IsNullOrWhiteSpace(scanAgentId)
            ? configuredScanAgentIds.FirstOrDefault() ?? "main"
            : scanAgentId.Trim();
        if (configuredScanAgentIds.Count > 0 &&
            !configuredScanAgentIds.Any(a => a.Equals(selectedScanAgentId, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["Error"] = "Selected OpenClaw scan agent is not allowed by configuration.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (project.BugScanStatus is ProjectBugScanStatus.Queued or ProjectBugScanStatus.InProgress)
        {
            TempData["Info"] = "Find Bugs is already running for this project.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            project.BugScanStatus = ProjectBugScanStatus.Queued;
            project.BugScanAgentId = selectedScanAgentId;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = $"Scan queued with '{selectedScanAgentId}'.";
            await _db.SaveChangesAsync();

            await _projectBugScanQueueService.EnqueueAsync(new ProjectBugScanQueueItem(
                project.Id,
                selectedScanAgentId,
                userId),
                HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            project.BugScanStatus = ProjectBugScanStatus.Failed;
            project.BugScanLastRunAt = DateTime.UtcNow;
            project.BugScanLastMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            await _db.SaveChangesAsync();
            TempData["Error"] = "Unable to queue Find Bugs scan.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["Success"] = $"Find Bugs queued with '{selectedScanAgentId}'. Tracking started.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddFeature(int crId, string name, string? description)
    {
        var userId = _userManager.GetUserId(User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var (isReached, freeLimit) = await HasReachedFreeFeatureLimitAsync(userId);
            if (isReached)
            {
                TempData["Error"] = $"Free plan allows up to {freeLimit} features. Upgrade to Pro to create more.";
                return RedirectToAction("Index", "Payment");
            }
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Feature name is required.";
            return RedirectToAction(nameof(Details), new { id = crId });
        }

        var cr = await _db.ChangeRequests.FindAsync(crId);
        if (cr == null) return NotFound();

        _db.ProjectFeatures.Add(new ProjectFeature
        {
            ChangeRequestId = crId,
            FeatureNumber = await GenerateNextFeatureNumberAsync(DateTime.UtcNow.Year),
            Name = name.Trim(),
            Description = description?.Trim(),
            ModuleImpacted = "General",
            Status = CrStatus.Draft,
            Priority = CrPriority.Medium,
            Stage = CrStage.Development,
            IsCompleted = false
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Feature \"{name.Trim()}\" added.";
        return RedirectToAction(nameof(Details), new { id = crId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFeature(int featureId, string? returnUrl)
    {
        var feature = await _db.ProjectFeatures.FindAsync(featureId);
        if (feature == null) return NotFound();

        feature.IsCompleted = !feature.IsCompleted;
        feature.Status = feature.IsCompleted ? CrStatus.Done : CrStatus.InProgress;
        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Details), new { id = feature.ChangeRequestId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteFeature(int featureId, string? returnUrl)
    {
        var feature = await _db.ProjectFeatures
            .Include(f => f.Screenshots)
            .FirstOrDefaultAsync(f => f.Id == featureId);
        if (feature == null) return NotFound();

        var crId = feature.ChangeRequestId;
        foreach (var shot in feature.Screenshots)
            DeleteFeatureScreenshotFile(shot.FileName, env);

        _db.ProjectFeatures.Remove(feature);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Feature removed.";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(Details), new { id = crId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Developer,Agent")]
    public async Task<IActionResult> UploadFeatureScreenshot(int featureId, IFormFile file, string? returnUrl)
    {
        var feature = await _db.ProjectFeatures
            .Include(f => f.ChangeRequest)
            .FirstOrDefaultAsync(f => f.Id == featureId);
        if (feature == null) return NotFound();
        if (!await CanViewProjectAsync(feature.ChangeRequestId))
            return Forbid();

        var userId = _userManager.GetUserId(User);
        if (!CanManageFeatureEvidence(feature, userId))
            return Forbid();

        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a screenshot file.";
            return RedirectFeatureLocal(returnUrl, featureId);
        }

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
        {
            TempData["Error"] = "Only image files (jpg, png, gif, webp) are allowed.";
            return RedirectFeatureLocal(returnUrl, featureId);
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            TempData["Error"] = "Screenshot must be under 10 MB.";
            return RedirectFeatureLocal(returnUrl, featureId);
        }

        var fileName = $"{featureId}_{Guid.NewGuid():N}{ext}";
        var folder = Path.Combine(env.WebRootPath, "uploads", "features", "screenshots");
        Directory.CreateDirectory(folder);
        var uploadPath = Path.Combine(folder, fileName);

        using (var stream = System.IO.File.Create(uploadPath))
            await file.CopyToAsync(stream);

        _db.FeatureScreenshots.Add(new FeatureScreenshot
        {
            ProjectFeatureId = featureId,
            FileName = fileName,
            OriginalFileName = Path.GetFileName(file.FileName)
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = "Feature screenshot uploaded.";
        return RedirectFeatureLocal(returnUrl, featureId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Developer,Agent")]
    public async Task<IActionResult> DeleteFeatureScreenshot(int screenshotId, int featureId, string? returnUrl)
    {
        var screenshot = await _db.FeatureScreenshots
            .Include(s => s.ProjectFeature)
            .FirstOrDefaultAsync(s => s.Id == screenshotId);
        if (screenshot == null) return NotFound();
        if (screenshot.ProjectFeatureId != featureId)
            return NotFound();
        if (screenshot.ProjectFeature == null)
            return NotFound();
        if (!await CanViewProjectAsync(screenshot.ProjectFeature.ChangeRequestId))
            return Forbid();

        var userId = _userManager.GetUserId(User);
        if (!CanManageFeatureEvidence(screenshot.ProjectFeature, userId))
            return Forbid();

        DeleteFeatureScreenshotFile(screenshot.FileName, env);
        _db.FeatureScreenshots.Remove(screenshot);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Feature screenshot deleted.";
        return RedirectFeatureLocal(returnUrl, featureId);
    }

    private async Task<List<ChangeRequest>> GetVisibleProjectsAsync(IQueryable<ChangeRequest> query)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        var companyName = await GetCurrentCompanyNameAsync();
        var userTeamId = await GetCurrentOrgTeamIdAsync();
        var isAdmin = User.IsInRole("Admin");
        return await ApplyProjectVisibility(query, userId, companyName, userTeamId, isAdmin).ToListAsync();
    }

    private async Task<(bool IsReached, int FreeLimit)> HasReachedFreeProjectLimitAsync(string userId)
    {
        var settings = await _systemSettingsService.GetProVersionSettingsAsync();
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || HasActiveProAccess(user))
            return (false, settings.FreeProjectLimit);

        var currentCount = await _db.ChangeRequests
            .CountAsync(c => c.CreatedById == userId);

        return (currentCount >= settings.FreeProjectLimit, settings.FreeProjectLimit);
    }

    private async Task<(bool IsReached, int FreeLimit)> HasReachedFreeFeatureLimitAsync(string userId)
    {
        var settings = await _systemSettingsService.GetProVersionSettingsAsync();
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || HasActiveProAccess(user))
            return (false, settings.FreeFeatureLimit);

        var currentCount = await _db.ChangeRequests
            .Where(c => c.CreatedById == userId)
            .SelectMany(c => c.Features)
            .CountAsync();

        return (currentCount >= settings.FreeFeatureLimit, settings.FreeFeatureLimit);
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

    private static (string ModuleKey, bool RequiresModify)? ResolvePermissionCheck(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
            return null;

        if (string.Equals(actionName, nameof(PickupFeature), StringComparison.OrdinalIgnoreCase))
            return (AppModuleKeys.Features, false);

        if (FeaturesViewActions.Contains(actionName))
            return (AppModuleKeys.Features, false);

        if (FeaturesModifyActions.Contains(actionName))
            return (AppModuleKeys.Features, true);

        if (AllProjectsViewActions.Contains(actionName))
            return (AppModuleKeys.AllProjects, false);

        return (AppModuleKeys.AllProjects, true);
    }

    private void DeleteImageFile(string fileName, IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.WebRootPath, "uploads", "archspec", fileName);
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    private void DeleteDocumentFile(string fileName, IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.WebRootPath, "uploads", "docs", fileName);
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    private void DeleteFeatureScreenshotFile(string fileName, IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.WebRootPath, "uploads", "features", "screenshots", fileName);
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    private bool CanManageFeatureEvidence(ProjectFeature feature, string? userId)
    {
        if (User.IsInRole("Admin"))
            return true;

        if (string.IsNullOrWhiteSpace(userId))
            return false;

        if (!(User.IsInRole("Developer") || User.IsInRole("Agent")))
            return false;

        return string.Equals(feature.AssignedDeveloperId, userId, StringComparison.Ordinal);
    }

    private async Task<List<SelectListItem>> GetEmployeeOptions()
    {
        var companyName = await GetCurrentCompanyNameAsync();
        var employees = FilterUsersByCompany(await _userManager.GetUsersInRoleAsync("Employee"), companyName);
        var developers = FilterUsersByCompany(await _userManager.GetUsersInRoleAsync("Developer"), companyName);
        var agents = FilterUsersByCompany(await _userManager.GetUsersInRoleAsync("Agent"), companyName);

        return employees.Select(e => new SelectListItem($"{e.FullName} (Employee)", e.Id))
            .Concat(developers.Select(e => new SelectListItem($"{e.FullName} (Developer)", e.Id)))
            .Concat(agents.Select(e => new SelectListItem($"{e.FullName} (Agent)", e.Id)))
            .OrderBy(e => e.Text)
            .ToList();
    }

    private async Task<List<SelectListItem>> GetProjectOptionsAsync()
    {
        var query = _db.ChangeRequests.AsQueryable();
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        var companyName = await GetCurrentCompanyNameAsync();
        var userTeamId = await GetCurrentOrgTeamIdAsync();
        var isAdmin = User.IsInRole("Admin");
        query = ApplyProjectVisibility(query, userId, companyName, userTeamId, isAdmin);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new SelectListItem($"{c.CrNumber} - {c.Title}", c.Id.ToString()))
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetEditableProjectOptionsAsync()
    {
        if (User.IsInRole("Admin"))
            return await GetProjectOptionsAsync();

        if (!User.IsInRole("Developer"))
            return [];

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return [];

        var companyName = await GetCurrentCompanyNameAsync();
        var userTeamId = await GetCurrentOrgTeamIdAsync();
        var query = ApplyProjectVisibility(_db.ChangeRequests.AsQueryable(), userId, companyName, userTeamId, isAdmin: false);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new SelectListItem($"{c.CrNumber} - {c.Title}", c.Id.ToString()))
            .ToListAsync();
    }

    private async Task<bool> CanEditFeatureProjectAsync(int changeRequestId)
    {
        if (User.IsInRole("Admin"))
            return await CanViewProjectAsync(changeRequestId);

        if (!User.IsInRole("Developer"))
            return false;

        return await CanViewProjectAsync(changeRequestId);
    }

    private async Task<int?> GetCurrentOrgTeamIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.OrgTeamId;
    }

    private async Task<string?> GetCurrentCompanyNameAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return NormalizeCompanyName(user?.CompanyName);
    }

    private static IQueryable<ChangeRequest> ApplyProjectVisibility(
        IQueryable<ChangeRequest> query,
        string userId,
        string? companyName,
        int? userTeamId,
        bool isAdmin)
    {
        if (!string.IsNullOrWhiteSpace(companyName))
            query = query.Where(c => c.CreatedBy != null && c.CreatedBy.CompanyName == companyName);
        else
            query = query.Where(c => c.CreatedById == userId);

        if (isAdmin)
            return query;

        if (userTeamId.HasValue)
            return query.Where(c => !c.OrgTeamId.HasValue || c.OrgTeamId == userTeamId.Value);

        return query.Where(c => !c.OrgTeamId.HasValue);
    }

    private async Task<bool> CanViewProjectAsync(int changeRequestId)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        var companyName = await GetCurrentCompanyNameAsync();
        var userTeamId = await GetCurrentOrgTeamIdAsync();
        var isAdmin = User.IsInRole("Admin");
        var query = ApplyProjectVisibility(_db.ChangeRequests.AsQueryable(), userId, companyName, userTeamId, isAdmin);
        return await query.AnyAsync(c => c.Id == changeRequestId);
    }

    private async Task<List<SelectListItem>> GetDeveloperOptionsAsync()
    {
        var companyName = await GetCurrentCompanyNameAsync();
        var developers = FilterUsersByCompany(await _userManager.GetUsersInRoleAsync("Developer"), companyName);
        var agents = FilterUsersByCompany(await _userManager.GetUsersInRoleAsync("Agent"), companyName);

        var usersById = new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase);
        var rolesByUser = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var user in developers)
        {
            usersById[user.Id] = user;
            if (!rolesByUser.TryGetValue(user.Id, out var roles))
            {
                roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                rolesByUser[user.Id] = roles;
            }
            roles.Add("Developer");
        }

        foreach (var user in agents)
        {
            usersById[user.Id] = user;
            if (!rolesByUser.TryGetValue(user.Id, out var roles))
            {
                roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                rolesByUser[user.Id] = roles;
            }
            roles.Add("Agent");
        }

        return usersById.Values
            .Select(user =>
            {
                var name = string.IsNullOrWhiteSpace(user.FullName) ? (user.Email ?? user.UserName ?? user.Id) : user.FullName;
                var roleLabel = rolesByUser.TryGetValue(user.Id, out var roles)
                    ? string.Join("/", roles.OrderBy(r => r))
                    : "Developer";
                return new SelectListItem($"{name} ({roleLabel})", user.Id);
            })
            .OrderBy(item => item.Text)
            .ToList();
    }

    private async Task<List<SelectListItem>> GetAgentUserOptionsAsync()
    {
        var companyName = await GetCurrentCompanyNameAsync();
        var agents = FilterUsersByCompany(await _userManager.GetUsersInRoleAsync("Agent"), companyName);
        return agents
            .OrderBy(a => a.FullName)
            .Select(a => new SelectListItem(a.FullName, a.Id))
            .ToList();
    }

    private static List<ApplicationUser> FilterUsersByCompany(IEnumerable<ApplicationUser> users, string? companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            return users.ToList();

        return users
            .Where(u => string.Equals(NormalizeCompanyName(u.CompanyName), companyName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string? NormalizeCompanyName(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task<bool> IsFeatureAssigneeValidAsync(ApplicationUser assignedUser)
    {
        return await _userManager.IsInRoleAsync(assignedUser, "Developer")
            || await _userManager.IsInRoleAsync(assignedUser, "Agent");
    }

    private async Task NotifyDeveloperFeatureAssignmentAsync(ProjectFeature feature, string? previousAssignedDeveloperId = null)
    {
        if (string.IsNullOrWhiteSpace(feature.AssignedDeveloperId))
            return;

        if (!string.IsNullOrWhiteSpace(previousAssignedDeveloperId) &&
            string.Equals(previousAssignedDeveloperId, feature.AssignedDeveloperId, StringComparison.OrdinalIgnoreCase))
            return;

        var assignedUser = await _userManager.FindByIdAsync(feature.AssignedDeveloperId);
        if (assignedUser == null || !await _userManager.IsInRoleAsync(assignedUser, "Developer"))
            return;

        var title = string.IsNullOrWhiteSpace(previousAssignedDeveloperId)
            ? $"New feature assigned: {feature.FeatureNumber}"
            : $"Feature assignment updated: {feature.FeatureNumber}";
        var message = $"You are assigned to feature {feature.FeatureNumber} - {feature.Name}.";
        var linkUrl = $"/ChangeRequest/FeatureDetails?featureId={feature.Id}";

        await _notificationService.CreateAsync(assignedUser.Id, title, message, linkUrl);
    }

    private async Task NotifyDeveloperProjectAssignmentsAsync(
        ChangeRequest project,
        IEnumerable<PicEntry> selectedPics,
        ISet<string>? previousAssignedEmployeeIds = null)
    {
        var selectedMap = selectedPics
            .Where(p => !string.IsNullOrWhiteSpace(p.EmployeeId))
            .GroupBy(p => p.EmployeeId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Role).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r)),
                StringComparer.OrdinalIgnoreCase);

        foreach (var (employeeId, role) in selectedMap)
        {
            if (previousAssignedEmployeeIds is not null &&
                previousAssignedEmployeeIds.Contains(employeeId))
                continue;

            var assignedUser = await _userManager.FindByIdAsync(employeeId);
            if (assignedUser == null || !await _userManager.IsInRoleAsync(assignedUser, "Developer"))
                continue;

            var roleText = string.IsNullOrWhiteSpace(role) ? "PIC" : role.Trim();
            var title = previousAssignedEmployeeIds is null
                ? $"New project assignment: {project.CrNumber}"
                : $"Project assignment updated: {project.CrNumber}";
            var message = $"You are assigned to project {project.CrNumber} - {project.Title} as {roleText}.";
            var linkUrl = $"/ChangeRequest/Details/{project.Id}";

            await _notificationService.CreateAsync(assignedUser.Id, title, message, linkUrl);
        }
    }

    private static void EnsureDefaultFeatureTimeline(CreateProjectFeatureViewModel model)
    {
        var startDate = model.TimelineStart?.Date ?? DateTime.Today;
        model.TimelineStart ??= startDate;
        model.TimelineEnd ??= startDate.AddDays(3);
    }

    private IActionResult RedirectFeatureLocal(string? returnUrl, int featureId)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction(nameof(FeatureDetails), new { featureId });
    }

    private List<SelectListItem> GetOpenClawScanAgentOptions()
    {
        var configured = OpenClawBugScanService.GetConfiguredAgentIds(_openClawSettings);
        if (configured.Count == 0)
            configured = ["main"];

        return configured
            .Select(id => new SelectListItem(id, id))
            .ToList();
    }

    private List<SelectListItem> GetOpenClawFeatureAgentOptions()
    {
        var configured = OpenClawBugScanService.GetConfiguredFeatureAgentIds(_openClawSettings);
        if (configured.Count == 0)
            configured = ["main"];

        return configured
            .Select(id => new SelectListItem(id, id))
            .ToList();
    }

    private void ApplyGitHubRepoFromUrl(ChangeRequestFormViewModel model)
    {
        model.GitHubRepoUrl = model.GitHubRepoUrl?.Trim();
        model.GitHubRepoOwner = model.GitHubRepoOwner?.Trim();
        model.GitHubRepoName = model.GitHubRepoName?.Trim();
        model.GitHubBranch = model.GitHubBranch?.Trim();
        model.TechnologyStack = model.TechnologyStack?.Trim();

        if (string.IsNullOrWhiteSpace(model.GitHubRepoUrl))
            return;

        if (!TryParseGitHubRepoUrl(model.GitHubRepoUrl, out var owner, out var repo, out var branchFromUrl))
        {
            ModelState.AddModelError(nameof(model.GitHubRepoUrl), "Invalid GitHub repository URL. Example: https://github.com/owner/repo");
            return;
        }

        model.GitHubRepoOwner = owner;
        model.GitHubRepoName = repo;
        if (string.IsNullOrWhiteSpace(model.GitHubBranch) && !string.IsNullOrWhiteSpace(branchFromUrl))
            model.GitHubBranch = branchFromUrl;
    }

    private bool TryResolveGitHubConfig(
        ChangeRequest project,
        out string owner,
        out string repo,
        out string? branch)
    {
        owner = project.GitHubRepoOwner?.Trim() ?? string.Empty;
        repo = project.GitHubRepoName?.Trim() ?? string.Empty;
        branch = project.GitHubBranch?.Trim();

        if ((!string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo)) ||
            string.IsNullOrWhiteSpace(project.GitHubRepoUrl))
        {
            return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo);
        }

        if (!TryParseGitHubRepoUrl(project.GitHubRepoUrl, out owner, out repo, out var parsedBranch))
            return false;

        if (string.IsNullOrWhiteSpace(branch))
            branch = parsedBranch;

        return true;
    }

    private static ChangeRequest BuildChangeRequestEntity(
        ChangeRequestFormViewModel model,
        string userId,
        string crNumber)
    {
        return new ChangeRequest
        {
            CrNumber = crNumber,
            Title = model.Title,
            Description = model.Description,
            Status = model.Status,
            Priority = model.Priority,
            Stage = model.Stage,
            FigmaLink = model.FigmaLink,
            ArchSpecLink = model.ArchSpecLink,
            ArchSpecNotes = model.ArchSpecNotes,
            TimelineStart = model.TimelineStart,
            TimelineEnd = model.TimelineEnd,
            GitHubRepoOwner = model.GitHubRepoOwner,
            GitHubRepoName = model.GitHubRepoName,
            GitHubRepoUrl = model.GitHubRepoUrl,
            GitHubBranch = model.GitHubBranch,
            TechnologyStack = model.TechnologyStack,
            CreatedById = userId
        };
    }

    private async Task<string> GenerateNextProjectNumberAsync(int year)
    {
        var prefix = $"PRJ-{year}-";
        var existingNumbers = await _db.ChangeRequests
            .Where(c => c.CrNumber.StartsWith(prefix))
            .Select(c => c.CrNumber)
            .ToListAsync();

        var maxSequence = 0;
        foreach (var value in existingNumbers)
        {
            if (!TryExtractRunningSequence(value, "PRJ", year, out var sequence))
                continue;

            if (sequence > maxSequence)
                maxSequence = sequence;
        }

        return $"{prefix}{(maxSequence + 1):D4}";
    }

    private async Task<string> GenerateNextFeatureNumberAsync(int year)
    {
        var prefix = $"CR-{year}-";
        var existingNumbers = await _db.ProjectFeatures
            .Where(f => f.FeatureNumber.StartsWith(prefix))
            .Select(f => f.FeatureNumber)
            .ToListAsync();

        var maxSequence = 0;
        foreach (var value in existingNumbers)
        {
            if (!TryExtractRunningSequence(value, "CR", year, out var sequence))
                continue;

            if (sequence > maxSequence)
                maxSequence = sequence;
        }

        return $"{prefix}{(maxSequence + 1):D4}";
    }

    private static bool TryExtractRunningSequence(string value, string expectedPrefix, int year, out int sequence)
    {
        sequence = 0;
        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return false;
        if (!parts[0].Equals(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!int.TryParse(parts[1], out var parsedYear) || parsedYear != year)
            return false;

        return int.TryParse(parts[2], out sequence);
    }

    private static bool IsDuplicateCrNumberException(DbUpdateException ex)
    {
        if (ex.InnerException is not SqlException sqlEx)
            return false;

        var isDuplicateIndex = sqlEx.Number == 2601 || sqlEx.Number == 2627;
        return isDuplicateIndex &&
               sqlEx.Message.Contains("IX_ChangeRequests_CrNumber", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshProjectFromGitHubAsync(
        ChangeRequest project,
        string owner,
        string repo,
        string? branch)
    {
        var repoInfo = await _gitHub.GetRepositoryInfoAsync(owner, repo);
        var resolvedBranch = string.IsNullOrWhiteSpace(branch) ? repoInfo.DefaultBranch : branch;

        var tree = await _gitHub.GetRepoTreeAsync(owner, repo, resolvedBranch);
        var detectedFeatures = FeatureDetector.DetectFeatures(tree);
        var languages = await _gitHub.GetRepositoryLanguagesAsync(owner, repo);
        var readme = await _gitHub.GetReadmeContentAsync(owner, repo);

        var readmeDescription = ExtractReadmeDescription(readme);
        var technologyStack = BuildTechnologyStack(languages);
        var description = !string.IsNullOrWhiteSpace(readmeDescription)
            ? readmeDescription
            : BuildRepoPurposeSummary(repoInfo.Name, repoInfo.Description, detectedFeatures, technologyStack);

        var figmaLink = FindFirstMatchingUrl(repoInfo.Homepage, readme, "figma.com");
        var archSpecLink = FindFirstMatchingUrl(repoInfo.Homepage, readme, "docs.google.com", "confluence", "notion.so", "miro.com");

        project.GitHubRepoOwner = owner;
        project.GitHubRepoName = repo;
        project.GitHubRepoUrl = repoInfo.HtmlUrl;
        project.GitHubBranch = resolvedBranch;
        project.TechnologyStack = technologyStack;

        if (!string.IsNullOrWhiteSpace(description))
            project.Description = description;
        if (!string.IsNullOrWhiteSpace(figmaLink))
            project.FigmaLink = figmaLink;
        if (!string.IsNullOrWhiteSpace(archSpecLink))
            project.ArchSpecLink = archSpecLink;
        if (string.IsNullOrWhiteSpace(project.Title))
            project.Title = HumanizeRepoName(repoInfo.Name);

        await SyncRepositoryFeaturesAsync(project.Id, detectedFeatures);
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task SyncRepositoryFeaturesAsync(
        int changeRequestId,
        IReadOnlyCollection<(string Name, string Description)> detectedFeatures)
    {
        var allFeatures = await _db.RepositoryFeatures
            .Where(f => f.ChangeRequestId == changeRequestId)
            .ToListAsync();

        var normalizedDetected = detectedFeatures
            .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToDictionary(f => f.Name, f => f.Description, StringComparer.OrdinalIgnoreCase);

        foreach (var feature in allFeatures)
        {
            if (normalizedDetected.TryGetValue(feature.Name, out var description))
            {
                feature.Description = description;
                feature.UpdatedAt = DateTime.UtcNow;
                normalizedDetected.Remove(feature.Name);
            }
            else
            {
                _db.RepositoryFeatures.Remove(feature);
            }
        }

        var existingNames = allFeatures
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, description) in normalizedDetected)
        {
            if (existingNames.Contains(name))
                continue;

            _db.RepositoryFeatures.Add(new RepositoryFeature
            {
                ChangeRequestId = changeRequestId,
                Name = name,
                Description = description,
                UpdatedAt = DateTime.UtcNow
            });
        }
    }

    private static string HumanizeRepoName(string repoName)
    {
        if (string.IsNullOrWhiteSpace(repoName))
            return repoName;

        var spaced = Regex.Replace(repoName.Trim(), @"[-_\.]+", " ");
        return Regex.Replace(spaced, @"\s{2,}", " ");
    }

    private static string BuildRepoPurposeSummary(
        string repoName,
        string? repoDescription,
        IReadOnlyCollection<(string Name, string Description)> features,
        string? technologyStack)
    {
        var cleanRepoName = HumanizeRepoName(repoName);
        var baseDescription = repoDescription?.Trim();
        var featureNames = features.Select(f => f.Name).Take(6).ToList();
        var languageHint = string.IsNullOrWhiteSpace(technologyStack)
            ? string.Empty
            : $" Primary technology stack: {technologyStack}.";

        if (!string.IsNullOrWhiteSpace(baseDescription) && featureNames.Count == 0)
            return $"{baseDescription}{languageHint}";

        if (!string.IsNullOrWhiteSpace(baseDescription) && featureNames.Count > 0)
            return $"{baseDescription} This repo appears to include: {string.Join(", ", featureNames)}.{languageHint}";

        if (featureNames.Count > 0)
            return $"{cleanRepoName} appears to be a software project that includes: {string.Join(", ", featureNames)}.{languageHint}";

        return $"{cleanRepoName} appears to be a software repository with application source code and project assets.{languageHint}";
    }

    private static string? BuildTechnologyStack(IReadOnlyDictionary<string, long> languages)
    {
        if (languages.Count == 0)
            return null;

        var totalBytes = languages.Values.Sum();
        if (totalBytes <= 0)
            return string.Join(", ", languages.Keys.OrderBy(k => k).Take(8));

        var topLanguages = languages
            .OrderByDescending(l => l.Value)
            .Take(8)
            .Select(l =>
            {
                var pct = (int)Math.Round(l.Value * 100.0 / totalBytes);
                return pct > 0 ? $"{l.Key} ({pct}%)" : l.Key;
            });

        return string.Join(", ", topLanguages);
    }

    private static string? ExtractReadmeDescription(string? readme)
    {
        if (string.IsNullOrWhiteSpace(readme))
            return null;

        var lines = readme.Replace("\r\n", "\n").Split('\n');
        var paragraphs = new List<string>();
        var currentParagraph = new List<string>();
        var inCodeBlock = false;

        void FlushParagraph()
        {
            if (currentParagraph.Count == 0)
                return;

            var paragraph = Regex.Replace(string.Join(" ", currentParagraph), @"\s{2,}", " ").Trim();
            if (!string.IsNullOrWhiteSpace(paragraph))
                paragraphs.Add(paragraph);

            currentParagraph.Clear();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.StartsWith("```") || line.StartsWith("~~~"))
            {
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
                continue;

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                continue;
            }

            if (IsReadmeNoiseLine(line))
            {
                FlushParagraph();
                continue;
            }

            var normalizedLine = Regex.Replace(line, @"^\s*>\s*", string.Empty);
            normalizedLine = Regex.Replace(normalizedLine, @"^\s*[-*+]\s+", string.Empty);
            normalizedLine = Regex.Replace(normalizedLine, @"^\s*\d+\.\s+", string.Empty);

            if (!string.IsNullOrWhiteSpace(normalizedLine))
                currentParagraph.Add(normalizedLine);
        }

        FlushParagraph();

        var best = paragraphs.FirstOrDefault(p => p.Length >= 40) ?? paragraphs.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(best))
            return null;

        return best.Length > 1500 ? best[..1500].Trim() : best;
    }

    private static bool IsReadmeNoiseLine(string line)
    {
        if (line.StartsWith("#"))
            return true;

        if (line.StartsWith("[![") || line.StartsWith("![") || line.StartsWith("<!--"))
            return true;

        if (line.StartsWith("<img", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("<picture", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("<p ", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("<div ", StringComparison.OrdinalIgnoreCase))
            return true;

        if (line == "---" || line.StartsWith("|"))
            return true;

        return false;
    }

    private static string? FindFirstMatchingUrl(string? homepage, string? readme, params string[] domainHints)
    {
        var urls = new List<string>();

        if (!string.IsNullOrWhiteSpace(homepage))
            urls.Add(homepage);

        if (!string.IsNullOrWhiteSpace(readme))
        {
            var matches = Regex.Matches(readme, "https?://[^\\s\\)\\]\\\"'>]+", RegexOptions.IgnoreCase);
            urls.AddRange(matches.Select(m => m.Value));
        }

        foreach (var url in urls)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                continue;

            if (domainHints.Length == 0 ||
                domainHints.Any(h => uri.Host.Contains(h, StringComparison.OrdinalIgnoreCase)))
            {
                return uri.ToString();
            }
        }

        return null;
    }

    private static bool TryParseGitHubRepoUrl(
        string url,
        out string owner,
        out string repo,
        out string? branch)
    {
        owner = string.Empty;
        repo = string.Empty;
        branch = null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var path = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return false;

        owner = segments[0];
        repo = Regex.Replace(segments[1], @"\.git$", string.Empty, RegexOptions.IgnoreCase);
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            return false;

        // Supports URLs like /owner/repo/tree/main or /owner/repo/tree/feature/my-branch
        if (segments.Length >= 4 && string.Equals(segments[2], "tree", StringComparison.OrdinalIgnoreCase))
        {
            branch = string.Join('/', segments.Skip(3));
        }

        return true;
    }

}

