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
    private readonly IOpenClawBugScanService _openClawService;

    public QaController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ISystemSettingsService settingsService,
        IOpenClawBugScanService openClawService)
    {
        _db = db;
        _userManager = userManager;
        _settingsService = settingsService;
        _openClawService = openClawService;
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

        var created = 0;
        foreach (var bug in newBugs)
        {
            var testNumber = await GenerateTestNumberAsync();
            var tc = new TestCase
            {
                TestNumber = testNumber,
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

        var project = await _db.ChangeRequests.FindAsync(projectId);
        if (project == null)
        {
            TempData["Error"] = "Project not found.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var result = await _openClawService.ScanProjectAsync(project, scanAgentId);
            if (!result.Succeeded)
            {
                TempData["Error"] = $"Scan failed: {result.Error}";
                return RedirectToAction(nameof(Index));
            }

            if (result.Findings.Count == 0)
            {
                TempData["Info"] = "Scan completed but found no issues. No test cases generated.";
                return RedirectToAction(nameof(Index));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var created = 0;

            foreach (var finding in result.Findings)
            {
                var testNumber = await GenerateTestNumberAsync();
                var tc = new TestCase
                {
                    TestNumber = testNumber,
                    Name = finding.Title,
                    Description = $"{finding.Description}\n\nWorkflow: {finding.Workflow}\n\nSteps to reproduce: {finding.StepsToReproduce}",
                    Module = finding.ModuleImpacted,
                    Status = TestCaseStatus.Pending,
                    Category = TestCaseCategory.Regression,
                    Environment = TestCaseEnvironment.Dev,
                    ChangeRequestId = projectId,
                    CreatedById = userId,
                    IsAutoGenerated = true
                };
                _db.TestCases.Add(tc);
                created++;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Scan found {result.Findings.Count} issue(s). {created} test case(s) generated.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Scan error: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoGenerate(int projectId, string? scanAgentId)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var project = await _db.ChangeRequests
            .Include(c => c.Features)
            .Include(c => c.RepositoryFeatures)
            .FirstOrDefaultAsync(c => c.Id == projectId);

        if (project == null)
        {
            TempData["Error"] = "Project not found.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var result = await _openClawService.GenerateTestCasesAsync(project, scanAgentId);
            if (!result.Succeeded)
            {
                TempData["Error"] = $"Auto-generate failed: {result.Error}";
                return RedirectToAction(nameof(Index));
            }

            if (result.TestCases.Count == 0)
            {
                TempData["Info"] = "OpenClaw analysed the project but generated no test cases.";
                return RedirectToAction(nameof(Index));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var created = 0;

            foreach (var gen in result.TestCases)
            {
                var testNumber = await GenerateTestNumberAsync();

                var category = Enum.TryParse<TestCaseCategory>(gen.Category, true, out var parsedCat)
                    ? parsedCat : TestCaseCategory.Regression;
                var environment = Enum.TryParse<TestCaseEnvironment>(gen.Environment, true, out var parsedEnv)
                    ? parsedEnv : TestCaseEnvironment.Dev;

                var tc = new TestCase
                {
                    TestNumber = testNumber,
                    Name = gen.Name,
                    Description = gen.Description,
                    Module = gen.Module,
                    Status = TestCaseStatus.Pending,
                    Category = category,
                    Environment = environment,
                    ChangeRequestId = projectId,
                    CreatedById = userId,
                    IsAutoGenerated = true
                };
                _db.TestCases.Add(tc);
                created++;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = $"OpenClaw generated {created} module-level test case(s).";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Auto-generate error: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanModule(int id)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var tc = await _db.TestCases
            .Include(t => t.ChangeRequest).ThenInclude(cr => cr!.Features)
            .Include(t => t.ChangeRequest).ThenInclude(cr => cr!.RepositoryFeatures)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (tc == null) return NotFound();

        if (tc.ChangeRequest == null)
        {
            TempData["Error"] = "Test case has no project linked — cannot scan.";
            return RedirectToAction(nameof(Index));
        }

        var hasModule = !string.IsNullOrWhiteSpace(tc.Module);

        try
        {
            var result = hasModule
                ? await _openClawService.ScanModuleAsync(tc.ChangeRequest, tc.Module!)
                : await _openClawService.ScanProjectAsync(tc.ChangeRequest);

            if (!result.Succeeded)
            {
                TempData["Error"] = $"Scan failed: {result.Error}";
                return RedirectToAction(nameof(Index));
            }

            var scanLabel = hasModule ? $"Module \"{tc.Module}\"" : "Project";
            if (result.Findings.Count == 0)
            {
                TempData["Info"] = $"{scanLabel} scan completed but found no issues.";
                return RedirectToAction(nameof(Index));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var created = 0;

            foreach (var finding in result.Findings)
            {
                var testNumber = await GenerateTestNumberAsync();
                var newTc = new TestCase
                {
                    TestNumber = testNumber,
                    Name = finding.Title,
                    Description = $"{finding.Description}\n\nWorkflow: {finding.Workflow}\n\nSteps to reproduce: {finding.StepsToReproduce}",
                    Module = finding.ModuleImpacted,
                    Status = TestCaseStatus.Pending,
                    Category = TestCaseCategory.Regression,
                    Environment = tc.Environment,
                    ChangeRequestId = tc.ChangeRequestId,
                    CreatedById = userId,
                    IsAutoGenerated = true
                };
                _db.TestCases.Add(newTc);
                created++;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = $"{scanLabel} scan found {result.Findings.Count} issue(s). {created} test case(s) generated.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Scan error: {ex.Message}";
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
        var year = DateTime.UtcNow.Year;
        var prefix = $"TC-{year}-";
        var lastTc = await _db.TestCases
            .Where(t => t.TestNumber.StartsWith(prefix))
            .OrderByDescending(t => t.TestNumber)
            .Select(t => t.TestNumber)
            .FirstOrDefaultAsync();

        var seq = 1;
        if (lastTc != null)
        {
            var parts = lastTc.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var lastSeq))
                seq = lastSeq + 1;
        }

        return $"{prefix}{seq:D4}";
    }
}
