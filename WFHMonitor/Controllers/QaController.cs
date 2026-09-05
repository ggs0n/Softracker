using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize]
public class QaController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISystemSettingsService _settingsService;
    private readonly IQaCodexQueueService _qaCodexQueueService;
    private readonly CodexSettings _codexSettings;

    public QaController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ISystemSettingsService settingsService,
        IQaCodexQueueService qaCodexQueueService,
        IOptions<CodexSettings> codexSettings)
    {
        _db = db;
        _userManager = userManager;
        _settingsService = settingsService;
        _qaCodexQueueService = qaCodexQueueService;
        _codexSettings = codexSettings.Value ?? new CodexSettings();
    }

    public async Task<IActionResult> Index(string? category, string? status, int? projectId, string? tab, string? scanAgentId)
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
        var activeTab = string.Equals(tab, "security", StringComparison.OrdinalIgnoreCase)
            ? "security"
            : "tests";

        var query = _db.TestCases
            .Include(t => t.ChangeRequest)
            .Include(t => t.LinkedBug)
            .Where(t => !t.ChangeRequestId.HasValue || projectIds.Contains(t.ChangeRequestId.Value))
            .AsNoTracking()
            .AsQueryable();

        if (activeTab == "security")
            query = WhereSecurityTagged(query);

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

        var projectScopedBugs = await _db.BugReports
            .AsNoTracking()
            .Where(b => b.ChangeRequestId.HasValue && projectIds.Contains(b.ChangeRequestId.Value))
            .ToListAsync();

        var relatedBugCounts = testCases.ToDictionary(
            tc => tc.Id,
            tc => CountRelatedBugs(tc, projectScopedBugs));

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

        var scanAgentOptions = CodexBugScanService.GetConfiguredAgentIds(_codexSettings)
            .Select(agentId => new SelectOptionItem
            {
                Value = agentId,
                Text = agentId
            })
            .ToList();
        var selectedScanAgentId = scanAgentOptions.Any(o => string.Equals(o.Value, scanAgentId, StringComparison.OrdinalIgnoreCase))
            ? scanAgentId?.Trim() ?? string.Empty
            : scanAgentOptions.FirstOrDefault()?.Value ?? string.Empty;

        var vm = new QaIndexViewModel
        {
            TestCases = testCases,
            Projects = projects,
            LinkedBugs = linkedBugs,
            RelatedBugCounts = relatedBugCounts,
            TotalCount = testCases.Count,
            PassedCount = testCases.Count(t => t.Status == TestCaseStatus.Pass),
            FailedCount = testCases.Count(t => t.Status == TestCaseStatus.Fail),
            PendingCount = testCases.Count(t => t.Status == TestCaseStatus.Pending),
            SkipCount = testCases.Count(t => t.Status == TestCaseStatus.Skip),
            ModuleCoverages = moduleCoverages,
            FilterCategory = category,
            FilterStatus = status,
            FilterProjectId = projectId,
            ActiveTab = activeTab,
            SelectedScanAgentId = selectedScanAgentId,
            CodexScanAgentOptions = scanAgentOptions
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

        var relatedBugs = new List<BugReport>();
        if (testCase.ChangeRequestId.HasValue)
        {
            var testCaseProjectId = testCase.ChangeRequestId.Value;

            relatedBugs = await _db.BugReports
                .Include(b => b.ChangeRequest)
                .AsNoTracking()
                .Where(b =>
                    b.ChangeRequestId == testCaseProjectId &&
                    (
                        (testCase.LinkedBugId.HasValue && b.Id == testCase.LinkedBugId.Value) ||
                        (!string.IsNullOrWhiteSpace(testNumber) &&
                            ((b.ChangeRequestReferenceText != null && b.ChangeRequestReferenceText.Contains(testNumber)) ||
                             (b.Description != null && b.Description.Contains(testNumber)) ||
                             (b.StepsToReproduce != null && b.StepsToReproduce.Contains(testNumber)))) ||
                        (hasModule &&
                         b.ModuleImpacted != null &&
                         b.ModuleImpacted == module)
                    ))
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

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
        TempData["Info"] = "Generate-from-bugs is disabled. Use Auto Generate Test Cases.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanAndGenerate(int projectId, string? scanAgentId, bool useSecurityPrompt = false)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var redirectTab = useSecurityPrompt ? "security" : "tests";

        var projectExists = await _db.ChangeRequests
            .AsNoTracking()
            .AnyAsync(c => c.Id == projectId);
        if (!projectExists)
        {
            TempData["Error"] = "Project not found.";
            return RedirectToAction(nameof(Index), new { tab = redirectTab, projectId, scanAgentId });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        try
        {
            await _qaCodexQueueService.EnqueueAsync(
                new QaCodexQueueItem(
                    QaCodexQueueOperation.ScanAndGenerate,
                    userId,
                    ProjectId: projectId,
                    ScanAgentId: scanAgentId,
                    UseSecurityPrompt: useSecurityPrompt),
                HttpContext.RequestAborted);

            TempData["Success"] = useSecurityPrompt
                ? "Codex security scan queued. Bugs will be added in the background."
                : "Codex scan queued. Bugs will be added in the background.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Unable to queue scan: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { tab = redirectTab, projectId, scanAgentId });
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
            await _qaCodexQueueService.EnqueueAsync(
                new QaCodexQueueItem(
                    QaCodexQueueOperation.AutoGenerate,
                    userId,
                    ProjectId: projectId,
                    ScanAgentId: scanAgentId),
                HttpContext.RequestAborted);

            TempData["Success"] = "Codex auto-generate queued. Test cases will be created in the background.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Unable to queue auto-generate: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateFlow(int projectId, string? scanAgentId, string? mode)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var projectExists = await _db.ChangeRequests
            .AsNoTracking()
            .AnyAsync(c => c.Id == projectId);
        if (!projectExists)
        {
            TempData["Error"] = "Project not found.";
            return RedirectToAction(nameof(Index), new { tab = "tests", projectId, scanAgentId });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var normalizedMode = string.Equals(mode, "generate_only", StringComparison.OrdinalIgnoreCase)
            ? "generate_only"
            : "generate_and_scan";

        try
        {
            await _qaCodexQueueService.EnqueueAsync(
                new QaCodexQueueItem(
                    QaCodexQueueOperation.AutoGenerate,
                    userId,
                    ProjectId: projectId,
                    ScanAgentId: scanAgentId),
                HttpContext.RequestAborted);

            if (normalizedMode == "generate_and_scan")
            {
                await _qaCodexQueueService.EnqueueAsync(
                    new QaCodexQueueItem(
                        QaCodexQueueOperation.ScanAndGenerate,
                        userId,
                        ProjectId: projectId,
                        ScanAgentId: scanAgentId),
                    HttpContext.RequestAborted);

                TempData["Success"] = "Codex queued: generated test cases and auto-scan-all.";
            }
            else
            {
                TempData["Success"] = "Codex queued: generate-only flow.";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Unable to queue generate flow: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { tab = "tests", projectId, scanAgentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RunSecurityScan(int projectId, string? scanAgentId, string? mode)
    {
        if (!await _settingsService.CanModifyModuleAsync(User, AppModuleKeys.QaTesting))
            return Forbid();

        var projectExists = await _db.ChangeRequests
            .AsNoTracking()
            .AnyAsync(c => c.Id == projectId);
        if (!projectExists)
        {
            TempData["Error"] = "Project not found.";
            return RedirectToAction(nameof(Index), new { tab = "security", projectId, scanAgentId });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var normalizedMode = string.Equals(mode, "full_pass", StringComparison.OrdinalIgnoreCase)
            ? "full_pass"
            : "security_tagged_only";

        try
        {
            if (normalizedMode == "full_pass")
            {
                await _qaCodexQueueService.EnqueueAsync(
                    new QaCodexQueueItem(
                        QaCodexQueueOperation.ScanAndGenerate,
                        userId,
                        ProjectId: projectId,
                        ScanAgentId: scanAgentId,
                        UseSecurityPrompt: true),
                    HttpContext.RequestAborted);

                TempData["Success"] = "Codex security scan queued for full project pass.";
            }
            else
            {
                var taggedCaseIds = await WhereSecurityTagged(
                        _db.TestCases
                            .AsNoTracking()
                            .Where(t => t.ChangeRequestId == projectId))
                    .Select(t => t.Id)
                    .ToListAsync(HttpContext.RequestAborted);

                if (taggedCaseIds.Count == 0)
                {
                    TempData["Info"] = "No security-tagged test cases found for this project.";
                    return RedirectToAction(nameof(Index), new { tab = "security", projectId, scanAgentId });
                }

                foreach (var id in taggedCaseIds)
                {
                    await _qaCodexQueueService.EnqueueAsync(
                        new QaCodexQueueItem(
                            QaCodexQueueOperation.ScanModule,
                            userId,
                            TestCaseId: id,
                            ScanAgentId: scanAgentId,
                            UseSecurityPrompt: true),
                        HttpContext.RequestAborted);
                }

                TempData["Success"] = $"Codex security scan queued for {taggedCaseIds.Count} security-tagged test case(s).";
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Unable to queue security scan: {ex.Message}";
        }

        return RedirectToAction(nameof(Index), new { tab = "security", projectId, scanAgentId });
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
            await _qaCodexQueueService.EnqueueAsync(
                new QaCodexQueueItem(
                    QaCodexQueueOperation.ScanModule,
                    userId,
                    TestCaseId: id),
                HttpContext.RequestAborted);

            TempData["Success"] = "Codex module scan queued. Bugs will be added in the background.";
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

    private static IQueryable<TestCase> WhereSecurityTagged(IQueryable<TestCase> query)
    {
        return query.Where(t =>
            (!string.IsNullOrEmpty(t.Name) &&
                (EF.Functions.Like(t.Name, "%security%") ||
                 EF.Functions.Like(t.Name, "%secure%") ||
                 EF.Functions.Like(t.Name, "%auth%") ||
                 EF.Functions.Like(t.Name, "%access control%") ||
                 EF.Functions.Like(t.Name, "%xss%") ||
                 EF.Functions.Like(t.Name, "%csrf%") ||
                 EF.Functions.Like(t.Name, "%sql injection%") ||
                 EF.Functions.Like(t.Name, "%owasp%"))) ||
            (!string.IsNullOrEmpty(t.Description) &&
                (EF.Functions.Like(t.Description, "%security%") ||
                 EF.Functions.Like(t.Description, "%secure%") ||
                 EF.Functions.Like(t.Description, "%auth%") ||
                 EF.Functions.Like(t.Description, "%access control%") ||
                 EF.Functions.Like(t.Description, "%xss%") ||
                 EF.Functions.Like(t.Description, "%csrf%") ||
                 EF.Functions.Like(t.Description, "%sql injection%") ||
                 EF.Functions.Like(t.Description, "%owasp%"))) ||
            (!string.IsNullOrEmpty(t.Module) &&
                (EF.Functions.Like(t.Module, "%security%") ||
                 EF.Functions.Like(t.Module, "%secure%") ||
                 EF.Functions.Like(t.Module, "%auth%") ||
                 EF.Functions.Like(t.Module, "%access%"))));
    }

    private async Task<string> GenerateTestNumberAsync()
    {
        var numbers = await GenerateTestNumbersAsync(1);
        return numbers[0];
    }

    private static int CountRelatedBugs(TestCase testCase, IReadOnlyCollection<BugReport> bugs)
    {
        if (bugs.Count == 0 || !testCase.ChangeRequestId.HasValue)
            return 0;

        var testNumber = testCase.TestNumber?.Trim();
        var module = testCase.Module?.Trim();
        var hasModule = !string.IsNullOrWhiteSpace(module);
        var testCaseProjectId = testCase.ChangeRequestId.Value;

        return bugs.Count(b => IsRelatedBug(b, testCase, testCaseProjectId, testNumber, module, hasModule));
    }

    private static bool IsRelatedBug(BugReport bug, TestCase testCase, int testCaseProjectId, string? testNumber, string? module, bool hasModule)
    {
        if (bug.ChangeRequestId != testCaseProjectId)
            return false;

        if (testCase.LinkedBugId.HasValue && bug.Id == testCase.LinkedBugId.Value)
            return true;

        if (!string.IsNullOrWhiteSpace(testNumber) &&
            (ContainsIgnoreCase(bug.ChangeRequestReferenceText, testNumber) ||
             ContainsIgnoreCase(bug.Description, testNumber) ||
             ContainsIgnoreCase(bug.StepsToReproduce, testNumber)))
        {
            return true;
        }

        if (hasModule &&
            !string.IsNullOrWhiteSpace(bug.ModuleImpacted) &&
            string.Equals(bug.ModuleImpacted.Trim(), module, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool ContainsIgnoreCase(string? haystack, string? needle)
    {
        if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needle))
            return false;

        return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
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
