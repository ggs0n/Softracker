using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;
using IWebHostEnvironment = Microsoft.AspNetCore.Hosting.IWebHostEnvironment;

namespace WFHMonitor.Services;

public class BugService : IBugService
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly string[] DocExtensions = [".pdf", ".docx", ".xlsx", ".xls", ".pptx", ".txt"];

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public BugService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        _db = db;
        _userManager = userManager;
        _env = env;
    }

    public async Task<List<BugReport>> GetIndexBugsAsync(bool forDeveloper, string? userId)
    {
        IQueryable<BugReport> query = _db.BugReports
            .Include(b => b.ChangeRequest)
            .Include(b => b.AssignedDeveloper)
            .Include(b => b.CreatedBy)
            .AsNoTracking();

        if (forDeveloper)
            query = query.Where(b => b.AssignedDeveloperId == userId);

        return await query
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<BugReport?> GetDetailsAsync(int id)
    {
        return await _db.BugReports
            .Include(b => b.ChangeRequest)
            .Include(b => b.AssignedDeveloper)
            .Include(b => b.CreatedBy)
            .Include(b => b.Screenshots.OrderBy(s => s.UploadedAt))
            .Include(b => b.Documents.OrderBy(d => d.UploadedAt))
            .Include(b => b.Activities.OrderBy(a => a.CreatedAt))
                .ThenInclude(a => a.OldAssignedDeveloper)
            .Include(b => b.Activities.OrderBy(a => a.CreatedAt))
                .ThenInclude(a => a.NewAssignedDeveloper)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public Task<BugReport?> GetByIdAsync(int id)
    {
        return _db.BugReports.FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task PopulateFormOptionsAsync(BugFormViewModel model)
    {
        model.DeveloperOptions = await GetDeveloperOptionsAsync();
        model.ChangeRequestOptions = await GetChangeRequestOptionsAsync();
    }

    public async Task<BugFormViewModel?> BuildEditViewModelAsync(int id)
    {
        var bug = await _db.BugReports.FirstOrDefaultAsync(b => b.Id == id);
        if (bug == null) return null;

        var vm = new BugFormViewModel
        {
            Id = bug.Id,
            Title = bug.Title,
            Description = bug.Description,
            Workflow = bug.Workflow,
            StepsToReproduce = bug.StepsToReproduce,
            ModuleImpacted = bug.ModuleImpacted,
            Status = bug.Status,
            AssigneeType = bug.AssigneeType,
            AgentStatus = bug.AgentStatus,
            ChangeRequestId = bug.ChangeRequestId,
            ChangeRequestReferenceText = bug.ChangeRequestReferenceText,
            AssignedDeveloperId = bug.AssignedDeveloperId
        };

        await PopulateFormOptionsAsync(vm);
        return vm;
    }

    public async Task<(bool Succeeded, string Error, int BugId)> CreateAsync(BugFormViewModel model, string createdById)
    {
        var bugNumber = await GenerateNextBugNumberAsync(DateTime.UtcNow.Year);
        var bug = new BugReport
        {
            BugNumber = bugNumber,
            Title = model.Title,
            Description = model.Description,
            Workflow = model.Workflow,
            StepsToReproduce = model.StepsToReproduce,
            ModuleImpacted = model.ModuleImpacted,
            Status = model.Status,
            AssigneeType = model.AssigneeType,
            AgentStatus = model.AssigneeType == BugAssigneeType.Agent ? BugAgentStatus.Queued : BugAgentStatus.None,
            ChangeRequestId = model.ChangeRequestId,
            ChangeRequestReferenceText = model.ChangeRequestReferenceText?.Trim(),
            AssignedDeveloperId = model.AssigneeType == BugAssigneeType.Agent ? null : model.AssignedDeveloperId,
            CreatedById = createdById
        };

        _db.BugReports.Add(bug);
        await _db.SaveChangesAsync();

        _db.BugActivities.Add(new BugActivity
        {
            BugReportId = bug.Id,
            Action = bug.AssigneeType == BugAssigneeType.Agent ? "Created (Assigned to Agent Queue)" : "Created",
            NewStatus = bug.Status,
            NewAssignedDeveloperId = bug.AssignedDeveloperId
        });

        if (model.DocumentFile != null)
        {
            var docResult = await SaveDocumentAsync(bug.Id, model.DocumentFile);
            if (!docResult.Succeeded)
                return (false, docResult.Error, 0);
        }

        await _db.SaveChangesAsync();
        return (true, string.Empty, bug.Id);
    }

    public async Task<(bool Succeeded, string Error)> UpdateAsync(int id, BugFormViewModel model)
    {
        var bug = await _db.BugReports.FirstOrDefaultAsync(b => b.Id == id);
        if (bug == null) return (false, "Bug not found.");

        var oldStatus = bug.Status;
        var oldAssignedId = bug.AssignedDeveloperId;
        var oldAssigneeType = bug.AssigneeType;
        var oldAgentStatus = bug.AgentStatus;

        bug.Title = model.Title;
        bug.Description = model.Description;
        bug.Workflow = model.Workflow;
        bug.StepsToReproduce = model.StepsToReproduce;
        bug.ModuleImpacted = model.ModuleImpacted;
        bug.Status = model.Status;
        bug.AssigneeType = model.AssigneeType;
        bug.AgentStatus = model.AssigneeType == BugAssigneeType.Agent
            ? (oldAssigneeType != BugAssigneeType.Agent ? BugAgentStatus.Queued : model.AgentStatus)
            : BugAgentStatus.None;
        bug.ChangeRequestId = model.ChangeRequestId;
        bug.ChangeRequestReferenceText = model.ChangeRequestReferenceText?.Trim();
        bug.AssignedDeveloperId = model.AssigneeType == BugAssigneeType.Agent ? null : model.AssignedDeveloperId;
        bug.UpdatedAt = DateTime.UtcNow;

        if (oldStatus != bug.Status ||
            oldAssignedId != bug.AssignedDeveloperId ||
            oldAssigneeType != bug.AssigneeType ||
            oldAgentStatus != bug.AgentStatus)
        {
            _db.BugActivities.Add(new BugActivity
            {
                BugReportId = bug.Id,
                Action = oldAssigneeType != bug.AssigneeType
                    ? $"Reassigned to {bug.AssigneeType}"
                    : "Updated",
                OldStatus = oldStatus,
                NewStatus = bug.Status,
                OldAssignedDeveloperId = oldAssignedId,
                NewAssignedDeveloperId = bug.AssignedDeveloperId
            });
        }

        await _db.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task UpdateStatusAsync(BugReport bug, BugStatus status)
    {
        if (bug.Status == status)
            return;

        var oldStatus = bug.Status;
        bug.Status = status;
        bug.UpdatedAt = DateTime.UtcNow;

        _db.BugActivities.Add(new BugActivity
        {
            BugReportId = bug.Id,
            Action = "Status Updated",
            OldStatus = oldStatus,
            NewStatus = bug.Status,
            OldAssignedDeveloperId = bug.AssignedDeveloperId,
            NewAssignedDeveloperId = bug.AssignedDeveloperId
        });

        await _db.SaveChangesAsync();
    }

    public async Task<(bool Succeeded, string Error)> UploadScreenshotAsync(int id, IFormFile file)
    {
        if (await _db.BugReports.FindAsync(id) == null)
            return (false, "Bug not found.");

        if (!ValidateFile(file, ImageExtensions, 10 * 1024 * 1024, "Screenshot", out var ext, out var error))
            return (false, error);

        var fileName = $"{id}_{Guid.NewGuid():N}{ext}";
        var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "bugs", "screenshots");
        Directory.CreateDirectory(uploadDir);
        var uploadPath = Path.Combine(uploadDir, fileName);

        using (var stream = System.IO.File.Create(uploadPath))
            await file.CopyToAsync(stream);

        _db.BugScreenshots.Add(new BugScreenshot
        {
            BugReportId = id,
            FileName = fileName,
            OriginalFileName = Path.GetFileName(file.FileName)
        });
        await _db.SaveChangesAsync();

        return (true, string.Empty);
    }

    public async Task<(bool Succeeded, string Error)> DeleteScreenshotAsync(int screenshotId)
    {
        var shot = await _db.BugScreenshots.FindAsync(screenshotId);
        if (shot == null) return (false, "Screenshot not found.");

        DeleteBugFile("screenshots", shot.FileName);
        _db.BugScreenshots.Remove(shot);
        await _db.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Succeeded, string Error)> UploadDocumentAsync(int id, IFormFile file)
    {
        if (await _db.BugReports.FindAsync(id) == null)
            return (false, "Bug not found.");

        var result = await SaveDocumentAsync(id, file);
        if (!result.Succeeded)
            return result;

        await _db.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Succeeded, string Error)> DeleteDocumentAsync(int documentId)
    {
        var doc = await _db.BugDocuments.FindAsync(documentId);
        if (doc == null) return (false, "Document not found.");

        DeleteBugFile("docs", doc.FileName);
        _db.BugDocuments.Remove(doc);
        await _db.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Succeeded, string Error)> DeleteBugAsync(int id)
    {
        var bug = await _db.BugReports
            .Include(b => b.Screenshots)
            .Include(b => b.Documents)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (bug == null) return (false, "Bug not found.");

        foreach (var shot in bug.Screenshots)
            DeleteBugFile("screenshots", shot.FileName);
        foreach (var doc in bug.Documents)
            DeleteBugFile("docs", doc.FileName);

        _db.BugReports.Remove(bug);
        await _db.SaveChangesAsync();
        return (true, string.Empty);
    }

    private async Task<List<SelectListItem>> GetDeveloperOptionsAsync()
    {
        var developers = await _userManager.GetUsersInRoleAsync("Developer");
        return developers
            .OrderBy(d => d.FullName)
            .Select(d => new SelectListItem(d.FullName, d.Id))
            .ToList();
    }

    private Task<List<SelectListItem>> GetChangeRequestOptionsAsync()
    {
        return _db.ChangeRequests
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new SelectListItem($"{c.CrNumber} - {c.Title}", c.Id.ToString()))
            .ToListAsync();
    }

    private async Task<(bool Succeeded, string Error)> SaveDocumentAsync(int bugId, IFormFile file)
    {
        if (!ValidateFile(file, DocExtensions, 20 * 1024 * 1024, "Document", out var ext, out var error))
            return (false, error);

        var fileName = $"{bugId}_{Guid.NewGuid():N}{ext}";
        var uploadDir = Path.Combine(_env.WebRootPath, "uploads", "bugs", "docs");
        Directory.CreateDirectory(uploadDir);
        var uploadPath = Path.Combine(uploadDir, fileName);

        using (var stream = System.IO.File.Create(uploadPath))
            await file.CopyToAsync(stream);

        _db.BugDocuments.Add(new BugDocument
        {
            BugReportId = bugId,
            FileName = fileName,
            OriginalFileName = Path.GetFileName(file.FileName)
        });

        return (true, string.Empty);
    }

    private void DeleteBugFile(string subFolder, string fileName)
    {
        var path = Path.Combine(_env.WebRootPath, "uploads", "bugs", subFolder, fileName);
        if (System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    private async Task<string> GenerateNextBugNumberAsync(int year)
    {
        var prefix = $"BUG-{year}-";

        var existingForYear = await _db.BugReports
            .AsNoTracking()
            .Where(b => b.BugNumber.StartsWith(prefix))
            .Select(b => b.BugNumber)
            .ToListAsync();

        var maxSequence = 0;

        foreach (var bugNumber in existingForYear)
        {
            if (bugNumber.Length <= prefix.Length)
                continue;

            var suffix = bugNumber[prefix.Length..];
            if (int.TryParse(suffix, out var sequence) && sequence > maxSequence)
                maxSequence = sequence;
        }

        return $"{prefix}{(maxSequence + 1):D4}";
    }

    private static bool ValidateFile(
        IFormFile? file,
        IReadOnlyCollection<string> allowedExtensions,
        long maxBytes,
        string label,
        out string ext,
        out string error)
    {
        ext = string.Empty;
        error = string.Empty;

        if (file == null || file.Length == 0)
        {
            error = $"Please select a {label.ToLowerInvariant()} file.";
            return false;
        }

        ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            error = label == "Screenshot"
                ? "Only image files (jpg, png, gif, webp) are allowed."
                : "Only PDF, Word, Excel, PowerPoint, or TXT files are allowed.";
            return false;
        }

        if (file.Length > maxBytes)
        {
            error = label == "Screenshot"
                ? "Screenshot must be under 10 MB."
                : "Document must be under 20 MB.";
            return false;
        }

        return true;
    }

}
