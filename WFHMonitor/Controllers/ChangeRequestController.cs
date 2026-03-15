using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IGitHubService _gitHub;
    private readonly IWebHostEnvironment env;

    public ChangeRequestController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IGitHubService gitHub,
        IWebHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _gitHub = gitHub;
        this.env = env;
    }

    public async Task<IActionResult> Index()
    {
        var query = _db.ChangeRequests
            .Include(c => c.CreatedBy)
            .Include(c => c.Pics).ThenInclude(p => p.Employee)
            .Include(c => c.Features)
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking();

        List<ChangeRequest> crs;
        if (User.IsInRole("Developer"))
        {
            var userId = _userManager.GetUserId(User);
            crs = await query
                .Where(c => c.Pics.Any(p => p.EmployeeId == userId))
                .ToListAsync();
        }
        else
        {
            crs = await query.ToListAsync();
        }

        return View(crs);
    }

    public async Task<IActionResult> Details(int id)
    {
        var projectForSync = await _db.ChangeRequests
            .AsTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (projectForSync == null) return NotFound();

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

        if (!ModelState.IsValid)
        {
            model.EmployeeOptions = await GetEmployeeOptions();
            return View(model);
        }

        var userId = _userManager.GetUserId(User)!;
        var currentYear = DateTime.UtcNow.Year;
        ChangeRequest? cr = null;
        const int maxCrNumberAttempts = 6;

        for (var attempt = 1; attempt <= maxCrNumberAttempts; attempt++)
        {
            var crNumberCandidate = await GenerateNextCrNumberAsync(currentYear);
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

        foreach (var pic in model.Pics.Where(p => !string.IsNullOrEmpty(p.EmployeeId)))
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

            _db.ProjectFeatures.Add(new ProjectFeature
            {
                ChangeRequestId = cr.Id,
                Name = featureName,
                Description = feature.Description?.Trim(),
                IsAutoDetected = feature.IsAutoDetected
            });
        }

        await _db.SaveChangesAsync();

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
        foreach (var pic in model.Pics.Where(p => !string.IsNullOrEmpty(p.EmployeeId)))
        {
            _db.ChangeRequestPics.Add(new ChangeRequestPic
            {
                ChangeRequestId = cr.Id,
                EmployeeId = pic.EmployeeId,
                Role = pic.Role
            });
        }

        await _db.SaveChangesAsync();
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
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cr == null) return NotFound();

        // Delete uploaded image files
        foreach (var img in cr.ArchSpecImages)
            DeleteImageFile(img.FileName, env);
        foreach (var doc in cr.Documents)
            DeleteDocumentFile(doc.FileName, env);

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

            var existingNames = await _db.ProjectFeatures
                .Where(f => f.ChangeRequestId == id)
                .Select(f => f.Name)
                .ToListAsync();

            var added = 0;
            foreach (var (name, description) in detected)
            {
                if (existingNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                _db.ProjectFeatures.Add(new ProjectFeature
                {
                    ChangeRequestId = id,
                    Name = name,
                    Description = description,
                    IsAutoDetected = true
                });
                added++;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = added > 0
                ? $"Scan complete — {added} feature(s) detected and added."
                : "Scan complete — no new features detected.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Scan failed: {ex.Message}";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddFeature(int crId, string name, string? description)
    {
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
            Name = name.Trim(),
            Description = description?.Trim()
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Feature \"{name.Trim()}\" added.";
        return RedirectToAction(nameof(Details), new { id = crId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFeature(int featureId)
    {
        var feature = await _db.ProjectFeatures.FindAsync(featureId);
        if (feature == null) return NotFound();

        feature.IsCompleted = !feature.IsCompleted;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = feature.ChangeRequestId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteFeature(int featureId)
    {
        var feature = await _db.ProjectFeatures.FindAsync(featureId);
        if (feature == null) return NotFound();

        var crId = feature.ChangeRequestId;
        _db.ProjectFeatures.Remove(feature);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Feature removed.";
        return RedirectToAction(nameof(Details), new { id = crId });
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

    private async Task<List<SelectListItem>> GetEmployeeOptions()
    {
        var employees = await _userManager.GetUsersInRoleAsync("Employee");
        var developers = await _userManager.GetUsersInRoleAsync("Developer");
        var agents = await _userManager.GetUsersInRoleAsync("Agent");

        return employees.Select(e => new SelectListItem($"{e.FullName} (Employee)", e.Id))
            .Concat(developers.Select(e => new SelectListItem($"{e.FullName} (Developer)", e.Id)))
            .Concat(agents.Select(e => new SelectListItem($"{e.FullName} (Agent)", e.Id)))
            .OrderBy(e => e.Text)
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

    private async Task<string> GenerateNextCrNumberAsync(int year)
    {
        var prefix = $"CR-{year}-";
        var existingNumbers = await _db.ChangeRequests
            .Where(c => c.CrNumber.StartsWith(prefix))
            .Select(c => c.CrNumber)
            .ToListAsync();

        var maxSequence = 0;
        foreach (var value in existingNumbers)
        {
            if (!TryExtractCrSequence(value, year, out var sequence))
                continue;

            if (sequence > maxSequence)
                maxSequence = sequence;
        }

        return $"{prefix}{(maxSequence + 1):D4}";
    }

    private static bool TryExtractCrSequence(string crNumber, int year, out int sequence)
    {
        sequence = 0;
        var parts = crNumber.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
            return false;
        if (!parts[0].Equals("CR", StringComparison.OrdinalIgnoreCase))
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

        await SyncAutoDetectedFeaturesAsync(project.Id, detectedFeatures);
        project.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task SyncAutoDetectedFeaturesAsync(
        int changeRequestId,
        IReadOnlyCollection<(string Name, string Description)> detectedFeatures)
    {
        var allFeatures = await _db.ProjectFeatures
            .Where(f => f.ChangeRequestId == changeRequestId)
            .ToListAsync();

        var autoFeatures = allFeatures
            .Where(f => f.IsAutoDetected)
            .ToList();

        var normalizedDetected = detectedFeatures
            .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToDictionary(f => f.Name, f => f.Description, StringComparer.OrdinalIgnoreCase);

        foreach (var feature in autoFeatures)
        {
            if (normalizedDetected.TryGetValue(feature.Name, out var description))
            {
                feature.Description = description;
                normalizedDetected.Remove(feature.Name);
            }
            else
            {
                _db.ProjectFeatures.Remove(feature);
            }
        }

        var existingNames = allFeatures
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, description) in normalizedDetected)
        {
            if (existingNames.Contains(name))
                continue;

            _db.ProjectFeatures.Add(new ProjectFeature
            {
                ChangeRequestId = changeRequestId,
                Name = name,
                Description = description,
                IsAutoDetected = true
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
