using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
                ViewBag.Commits = await _gitHub.GetCommitsAsync(
                    owner, repo, branch);
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
        var count = await _db.ChangeRequests.CountAsync();
        var crNumber = $"CR-{DateTime.UtcNow.Year}-{(count + 1):D4}";

        var cr = new ChangeRequest
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
            CreatedById = userId
        };

        _db.ChangeRequests.Add(cr);
        await _db.SaveChangesAsync();

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

        TempData["Success"] = $"Project {crNumber} created.";
        return RedirectToAction(nameof(Details), new { id = cr.Id });
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
