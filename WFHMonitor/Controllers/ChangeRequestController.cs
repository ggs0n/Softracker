using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
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

        if (User.IsInRole("Admin") || User.IsInRole("Tester"))
            return View(await query.ToListAsync());

        if (User.IsInRole("Developer"))
        {
            var userId = _userManager.GetUserId(User);
            var crs = await query
                .Where(c => c.Pics.Any(p => p.EmployeeId == userId))
                .ToListAsync();
            return View(crs);
        }

        return Forbid();
    }

    public async Task<IActionResult> Details(int id)
    {
        var cr = await _db.ChangeRequests
            .Include(c => c.CreatedBy)
            .Include(c => c.Pics).ThenInclude(p => p.Employee)
            .Include(c => c.ArchSpecImages.OrderBy(i => i.SortOrder))
            .Include(c => c.Documents.OrderBy(d => d.UploadedAt))
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cr == null) return NotFound();

        if (User.IsInRole("Developer"))
        {
            var userId = _userManager.GetUserId(User);
            if (!cr.Pics.Any(p => p.EmployeeId == userId))
                return Forbid();
        }
        else if (!User.IsInRole("Admin") && !User.IsInRole("Tester"))
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(cr.GitHubRepoOwner) &&
            !string.IsNullOrWhiteSpace(cr.GitHubRepoName) &&
            !string.IsNullOrWhiteSpace(cr.GitHubBranch))
        {
            try
            {
                ViewBag.Commits = await _gitHub.GetCommitsAsync(
                    cr.GitHubRepoOwner, cr.GitHubRepoName, cr.GitHubBranch);
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
        if (!ModelState.IsValid)
        {
            model.EmployeeOptions = await GetEmployeeOptions();
            return View(model);
        }

        var userId = _userManager.GetUserId(User)!;

        var cr = new ChangeRequest
        {
            CrNumber = await GenerateNextCrNumberAsync(),
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
            GitHubBranch = model.GitHubBranch,
            CreatedById = userId
        };

        _db.ChangeRequests.Add(cr);

        var saved = false;
        for (var attempt = 0; attempt < 3 && !saved; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync();
                saved = true;
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                _db.Entry(cr).Property(x => x.CrNumber).CurrentValue = await GenerateNextCrNumberAsync();
            }
        }

        if (!saved)
            throw new InvalidOperationException("Could not generate a unique CR number. Please try again.");

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

        TempData["Success"] = $"Change Request {cr.CrNumber} created.";
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
        if (id != model.Id) return BadRequest();

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
        TempData["Success"] = "Change Request updated.";
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
        TempData["Success"] = "Change Request deleted.";
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
        var uploadDir = Path.Combine(env.WebRootPath, "uploads", "archspec");
        Directory.CreateDirectory(uploadDir);
        var uploadPath = Path.Combine(uploadDir, fileName);

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
        if (img.ChangeRequestId != crId) return BadRequest();

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
        if (doc.ChangeRequestId != crId) return BadRequest();

        DeleteDocumentFile(doc.FileName, env);
        _db.ChangeRequestDocuments.Remove(doc);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Document deleted.";
        return RedirectToAction(nameof(Details), new { id = crId });
    }

    private async Task<string> GenerateNextCrNumberAsync()
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"CR-{year}-";

        var lastForYear = await _db.ChangeRequests
            .Where(c => c.CrNumber.StartsWith(prefix))
            .OrderByDescending(c => c.CrNumber)
            .Select(c => c.CrNumber)
            .FirstOrDefaultAsync();

        var next = 1;
        if (!string.IsNullOrWhiteSpace(lastForYear))
        {
            var suffix = lastForYear[prefix.Length..];
            if (int.TryParse(suffix, out var parsed))
                next = parsed + 1;
        }

        return $"{prefix}{next:D4}";
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

        return employees.Select(e => new SelectListItem($"{e.FullName} (Employee)", e.Id))
            .Concat(developers.Select(e => new SelectListItem($"{e.FullName} (Developer)", e.Id)))
            .OrderBy(e => e.Text)
            .ToList();
    }
}
