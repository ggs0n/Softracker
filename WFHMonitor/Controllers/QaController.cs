using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize]
public class QaController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISystemSettingsService _settingsService;
    private readonly IQaOpenClawQueueService _qaOpenClawQueueService;

    public QaController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ISystemSettingsService settingsService,
        IQaOpenClawQueueService qaOpenClawQueueService)
    {
        _db = db;
        _userManager = userManager;
        _settingsService = settingsService;
        _qaOpenClawQueueService = qaOpenClawQueueService;
    }

    public async Task<IActionResult> Index(string? category, string? status, int? projectId)
    {
        if (!await _settingsService.CanViewModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var currentUser = await _userManager.GetUserAsync(User);
        var companyName = currentUser?.CompanyName?.Trim();

        var projectsQuery = _db.ChangeRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(companyName))
            projectsQuery = projectsQuery.Where(c => c.CreatedBy != null && c.CreatedBy.CompanyName == companyName);
        else if (currentUser != null)
            projectsQuery = projectsQuery.Where(c => c.CreatedById == currentUser.Id);

        var projects = await projectsQuery.OrderByDescending(p => p.UpdatedAt).ToListAsync();
        var projectIds = projects.Select(p => p.Id).ToList();

        var query = _db.TestCases
            .Include(t => t.ChangeRequest)
            .Include(t => t.LinkedBug)
            .Where(t => !t.ChangeRequestId.HasValue || projectIds.Contains(t.ChangeRequestId.Value))
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<TestCaseCategory>(category, true, out var cat) && cat != TestCaseCategory.All)
            query = query.Where(t => t.Category == cat);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TestCaseStatus>(status, true, out var st))
            query = query.Where(t => t.Status == st);

        if (projectId.HasValue)
            query = query.Where(t => t.ChangeRequestId == projectId.Value);

        var testCases = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

        // Bug list for linked bugs sidebar
        var bugIds = testCases.Where(t => t.LinkedBugId.HasValue).Select(t => t.LinkedBugId!.Value).Distinct().ToList();
        var linkedBugs = bugIds.Count == 0
            ? new List<BugReport>()
            : await _db.BugReports.AsNoTracking().Where(b => bugIds.Contains(b.Id)).ToListAsync();

        // Module coverage
        var moduleCoverages = testCases
            .Where(t => !string.IsNullOrWhiteSpace(t.Module))
            .GroupBy(t => t.Module!)
            .Select(g => new ModuleCoverage
            {
                Module = g.Key,
                Total = g.Count(),
                Passed = g.Count(t => t.Status == TestCaseStatus.Pass)
            })
            .OrderByDescending(m => m.Total)
            .ToList();

        var vm = new QaIndexViewModel
        {
            TestCases = testCases,
            Projects = projects,
            LinkedBugs = linkedBugs,
            TotalCount = testCases.Count,
            PassedCount = testCases.Count(t => t.Status == TestCaseStatus.Pass),
            FailedCount = testCases.Count(t => t.Status == TestCaseStatus.Fail),
            PendingCount = testCases.Count(t => t.Status == TestCaseStatus.Pending),
            SkipCount = testCases.Count(t => t.Status == TestCaseStatus.Skip),
            ModuleCoverages = moduleCoverages,
            FilterCategory = category,
            FilterStatus = status,
            FilterProjectId = projectId
        };

        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (!await _settingsService.CanViewModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var currentUser = await _userManager.GetUserAsync(User);
        var companyName = currentUser?.CompanyName?.Trim();

        var projectsQuery = _db.ChangeRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(companyName))
            projectsQuery = projectsQuery.Where(c => c.CreatedBy != null && c.CreatedBy.CompanyName == companyName);
        else if (currentUser != null)
            projectsQuery = projectsQuery.Where(c => c.CreatedById == currentUser.Id);

        var allowedProjectIds = await projectsQuery.Select(p => p.Id).ToListAsync();

        var testCase = await _db.TestCases
            .Include(t => t.ChangeRequest)
            .Include(t => t.LinkedBug)
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (testCase == null)
            return NotFound();

        if (testCase.ChangeRequestId.HasValue && !allowedProjectIds.Contains(testCase.ChangeRequestId.Value))
            return Forbid();

        var testNumber = testCase.TestNumber ?? string.Empty;
        var module = testCase.Module ?? string.Empty;
        var hasModule = !string.IsNullOrWhiteSpace(module);

        var relatedBugs = await _db.BugReports
            .Include(b => b.ChangeRequest)
            .AsNoTracking()
            .Where(b =>
                (testCase.LinkedBugId.HasValue && b.Id == testCase.LinkedBugId.Value) ||
                (!string.IsNullOrWhiteSpace(testNumber) &&
                    ((b.ChangeRequestReferenceText != null && b.ChangeRequestReferenceText.Contains(testNumber)) ||
                     (b.Description != null && b.Description.Contains(testNumber)) ||
                     (b.StepsToReproduce != null && b.StepsToReproduce.Contains(testNumber)))) ||
                (testCase.ChangeRequestId.HasValue &&
                 b.ChangeRequestId == testCase.ChangeRequestId.Value &&
                 hasModule &&
                 b.ModuleImpacted != null &&
                 b.ModuleImpacted == module))
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        var vm = new QaTestCaseDetailsViewModel
        {
            TestCase = testCase,
            RelatedBugs = relatedBugs
        };

        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTestCase(TestCaseFormViewModel model)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            TempData["Error"] = "Test case name is required.";
            return RedirectToAction(nameof(Index));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var nextNumber = await GenerateTestNumberAsync();

        var testCase = new TestCase
        {
            TestNumber = nextNumber,
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            Module = model.Module?.Trim(),
            Status = model.Status,
            Category = model.Category,
            Environment = model.Environment,
            ChangeRequestId = model.ChangeRequestId,
            LinkedBugId = model.LinkedBugId,
            CreatedById = userId,
            IsAutoGenerated = false
        };

        _db.TestCases.Add(testCase);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Test case {nextNumber} created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TestCaseFormViewModel model)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var tc = await _db.TestCases.FindAsync(model.Id);
        if (tc == null) return NotFound();

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            TempData["Error"] = "Test case name is required.";
            return RedirectToAction(nameof(Index));
        }

        tc.Name = model.Name.Trim();
        tc.Description = model.Description?.Trim();
        tc.Module = model.Module?.Trim();
        tc.Status = model.Status;
        tc.Category = model.Category;
        tc.Environment = model.Environment;
        tc.ChangeRequestId = model.ChangeRequestId;
        tc.LinkedBugId = model.LinkedBugId;
        tc.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Test case {tc.TestNumber} updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, TestCaseStatus status)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var tc = await _db.TestCases.FindAsync(id);
        if (tc == null) return NotFound();

        tc.Status = status;
        tc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Test case {tc.TestNumber} updated to {status}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var tc = await _db.TestCases.FindAsync(id);
        if (tc == null) return NotFound();

        _db.TestCases.Remove(tc);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Test case {tc.TestNumber} deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateFromBugs(int projectId)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var bugs = await _db.BugReports
            .Where(b => b.ChangeRequestId == projectId)
            .AsNoTracking()
            .ToListAsync();

        if (bugs.Count == 0)
        {
            TempData["Error"] = "No bugs found for this project to generate test cases.";
            return RedirectToAction(nameof(Index));
        }

        // Get existing linked bug IDs to avoid duplicates
        var existingLinkedBugIds = await _db.TestCases
            .Where(t => t.ChangeRequestId == projectId && t.LinkedBugId.HasValue)
            .Select(t => t.LinkedBugId!.Value)
            .ToListAsync();

        var newBugs = bugs.Where(b => !existingLinkedBugIds.Contains(b.Id)).ToList();
        if (newBugs.Count == 0)
        {
            TempData["Info"] = "All bugs already have test cases generated.";
            return RedirectToAction(nameof(Index));
        }

        var testNumbers = await GenerateTestNumbersAsync(newBugs.Count);
        var created = 0;
        for (var i = 0; i < newBugs.Count; i++)
        {
            var bug = newBugs[i];
            var tc = new TestCase
            {
                TestNumber = testNumbers[i],
                Name = bug.Title,
                Description = !string.IsNullOrWhiteSpace(bug.StepsToReproduce)
                    ? $"Verify fix: {bug.Description}\n\nSteps: {bug.StepsToReproduce}"
                    : $"Verify fix: {bug.Description}",
                Module = bug.ModuleImpacted,
                Status = TestCaseStatus.Pending,
                Category = TestCaseCategory.Regression,
                Environment = TestCaseEnvironment.Dev,
                ChangeRequestId = projectId,
                LinkedBugId = bug.Id,
                CreatedById = userId,
                IsAutoGenerated = true
            };
            _db.TestCases.Add(tc);
            created++;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"{created} test case(s) generated from bugs.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanAndGenerate(int projectId, string? scanAgentId)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var projectExists = await _db.ChangeRequests
            .AsNoTracking()
            .AnyAsync(c => c.Id == projectId);
        if (!projectExists)
        {
            TempData["Error"] = "Project not found.";
            return RedirectToAction(nameof(Index));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        try
        {
            await _qaOpenClawQueueService.EnqueueAsync(
                new QaOpenClawQueueItem(
                    QaOpenClawQueueOperation.ScanAndGenerate,
                    userId,
                    ProjectId: projectId,
                    ScanAgentId: scanAgentId),
                HttpContext.RequestAborted);

            TempData["Success"] = "OpenClaw scan queued. Bugs will be added in the background.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Unable to queue scan: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoGenerate(int projectId, string? scanAgentId)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var projectExists = await _db.ChangeRequests
            .AsNoTracking()
            .AnyAsync(c => c.Id == projectId);
        if (!projectExists)
        {
            TempData["Error"] = "Project not found.";
            return RedirectToAction(nameof(Index));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        try
        {
            await _qaOpenClawQueueService.EnqueueAsync(
                new QaOpenClawQueueItem(
                    QaOpenClawQueueOperation.AutoGenerate,
                    userId,
                    ProjectId: projectId,
                    ScanAgentId: scanAgentId),
                HttpContext.RequestAborted);

            TempData["Success"] = "OpenClaw auto-generate queued. Test cases will be created in the background.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Unable to queue auto-generate: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanModule(int id)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var testCaseExists = await _db.TestCases
            .AsNoTracking()
            .AnyAsync(t => t.Id == id);
        if (!testCaseExists)
            return NotFound();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        try
        {
            await _qaOpenClawQueueService.EnqueueAsync(
                new QaOpenClawQueueItem(
                    QaOpenClawQueueOperation.ScanModule,
                    userId,
                    TestCaseId: id),
                HttpContext.RequestAborted);

            TempData["Success"] = "OpenClaw module scan queued. Bugs will be added in the background.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Unable to queue module scan: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAll(int? projectId)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        // Mark all pending test cases as "running" — in a real system this would trigger actual test execution
        // For now, this is a placeholder that updates the UI
        TempData["Info"] = "Test run queued. Results will appear when the agent completes.";
        return RedirectToAction(nameof(Index), new { projectId });
    }

    private async Task<string> GenerateTestNumberAsync()
    {
        var numbers = await GenerateTestNumbersAsync(1);
        return numbers[0];
    }

    private async Task<List<string>> GenerateTestNumbersAsync(int count)
    {
        if (count <= 0)
            return [];

        var year = DateTime.UtcNow.Year;
        var prefix = $"TC-{year}-";
        var existingNumbers = await _db.TestCases
            .Where(t => t.TestNumber.StartsWith(prefix))
            .Select(t => t.TestNumber)
            .ToListAsync();

        var trackedNumbers = _db.ChangeTracker
            .Entries<TestCase>()
            .Where(e => e.State != EntityState.Deleted
                && !string.IsNullOrWhiteSpace(e.Entity.TestNumber)
                && e.Entity.TestNumber.StartsWith(prefix, StringComparison.Ordinal))
            .Select(e => e.Entity.TestNumber);

        var maxSeq = existingNumbers
            .Concat(trackedNumbers)
            .Select(ParseTestNumberSequence)
            .DefaultIfEmpty(0)
            .Max();

        var numbers = new List<string>(count);
        for (var i = 1; i <= count; i++)
        {
            numbers.Add($"{prefix}{maxSeq + i:D4}");
        }

        return numbers;
    }

    private static int ParseTestNumberSequence(string? testNumber)
    {
        if (string.IsNullOrWhiteSpace(testNumber))
            return 0;

        var parts = testNumber.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3 && int.TryParse(parts[2], out var seq)
            ? seq
            : 0;
    }
}
