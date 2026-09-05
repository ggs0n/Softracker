using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public sealed class QaAiAutomationQueueService
    : BackgroundService, IQaAiAutomationQueueService
{
    private readonly Channel<QaAiAutomationQueueItem> _queue =
        Channel.CreateBounded<QaAiAutomationQueueItem>(
        new BoundedChannelOptions(50)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QaAiAutomationQueueService> _logger;

    public QaAiAutomationQueueService(
        IServiceScopeFactory scopeFactory,
        ILogger<QaAiAutomationQueueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task EnqueueAsync(
        QaAiAutomationQueueItem item,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(item.RequestedByUserId))
            throw new ArgumentException("Invalid QA AI automation queue item.");

        if (item.Operation is QaAiAutomationQueueOperation.ScanAndGenerate
            or QaAiAutomationQueueOperation.AutoGenerate)
        {
            if (!item.ProjectId.HasValue || item.ProjectId.Value <= 0)
                throw new ArgumentException("ProjectId is required for this operation.");
        }

        if (item.Operation == QaAiAutomationQueueOperation.ScanModule)
        {
            if (!item.TestCaseId.HasValue || item.TestCaseId.Value <= 0)
                throw new ArgumentException("TestCaseId is required for module scan.");
        }

        return _queue.Writer.WriteAsync(item, cancellationToken).AsTask();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessItemAsync(item, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "QA AI automation queue failed for operation {Operation}.",
                    item.Operation);
                await TryNotifyAsync(
                    item.RequestedByUserId,
                    "AI automation QA failed",
                    "The QA automation job failed unexpectedly. Please retry.",
                    "/Qa/Index");
            }
        }
    }

    private async Task ProcessItemAsync(
        QaAiAutomationQueueItem item,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var automation =
            scope.ServiceProvider.GetRequiredService<IAiAutomationService>();
        var workspaceService =
            scope.ServiceProvider.GetRequiredService<IRepositoryWorkspaceService>();
        var bugService = scope.ServiceProvider.GetRequiredService<IBugService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        switch (item.Operation)
        {
            case QaAiAutomationQueueOperation.ScanAndGenerate:
                await ProcessScanAndGenerateAsync(db, automation, workspaceService, bugService, notificationService, item, cancellationToken);
                break;
            case QaAiAutomationQueueOperation.AutoGenerate:
                await ProcessAutoGenerateAsync(db, automation, workspaceService, notificationService, item, cancellationToken);
                break;
            case QaAiAutomationQueueOperation.ScanModule:
                await ProcessScanModuleAsync(db, automation, workspaceService, bugService, notificationService, item, cancellationToken);
                break;
        }
    }

    private static string? NormalizeAgentId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task ProcessScanAndGenerateAsync(
        ApplicationDbContext db,
        IAiAutomationService automation,
        IRepositoryWorkspaceService workspaceService,
        IBugService bugService,
        INotificationService notificationService,
        QaAiAutomationQueueItem item,
        CancellationToken cancellationToken)
    {
        var projectId = item.ProjectId!.Value;
        var project = await db.ChangeRequests
            .Include(c => c.Features)
            .Include(c => c.RepositoryFeatures)
            .FirstOrDefaultAsync(c => c.Id == projectId, cancellationToken);

        if (project == null)
        {
            await NotifyAsync(
                notificationService,
                item.RequestedByUserId,
                "AI scan failed",
                "QA scan failed because the selected project no longer exists.",
                "/Qa/Index");
            return;
        }

        var createdById = await ResolveCreatedByUserIdAsync(db, item.RequestedByUserId, project.CreatedById, cancellationToken);
        if (string.IsNullOrWhiteSpace(createdById))
            return;

        var workspace = await workspaceService.PrepareReadOnlyAsync(
            project,
            item.RequestedByUserId,
            $"qa-scan-{project.CrNumber}",
            cancellationToken);
        var result = await automation.ScanProjectAsync(
            project,
            workspace.RepositoryDirectory,
            cancellationToken,
            item.UseSecurityPrompt);
        var scanLabel = item.UseSecurityPrompt ? "AI security scan" : "AI scan";
        if (!result.Succeeded)
        {
            await NotifyAsync(
                notificationService,
                item.RequestedByUserId,
                $"{scanLabel} failed",
                $"{scanLabel} failed for {project.CrNumber}: {TrimTo(result.Error, 600)}",
                $"/Qa/Index?projectId={project.Id}");
            return;
        }

        if (result.Findings.Count == 0)
        {
            await NotifyAsync(
                notificationService,
                item.RequestedByUserId,
                $"{scanLabel} completed",
                $"{scanLabel} completed for {project.CrNumber} with no findings.",
                $"/Qa/Index?projectId={project.Id}");
            return;
        }

        var createdBugs = 0;
        var skippedDuplicateBugs = 0;
        var bugCreateErrors = 0;

        var existingBugTitles = await db.BugReports
            .AsNoTracking()
            .Where(b => b.ChangeRequestId == project.Id)
            .Select(b => b.Title)
            .ToListAsync(cancellationToken);
        var knownBugTitles = existingBugTitles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in result.Findings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedTitle = (finding.Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedTitle))
                continue;

            if (!knownBugTitles.Add(normalizedTitle))
            {
                skippedDuplicateBugs++;
                continue;
            }

            var bugModel = new BugFormViewModel
            {
                Title = normalizedTitle,
                Description = TrimTo(finding.Description, 4000),
                Workflow = TrimTo(finding.Workflow, 4000),
                StepsToReproduce = TrimTo(finding.StepsToReproduce, 4000),
                ModuleImpacted = TrimTo(finding.ModuleImpacted, 200),
                Severity = BugSeverityResolver.Resolve(
                    finding.Severity,
                    finding.Title,
                    finding.Description,
                    finding.Workflow,
                    finding.StepsToReproduce,
                    finding.ModuleImpacted),
                Status = BugStatus.New,
                AssigneeType = BugAssigneeType.Developer,
                ChangeRequestId = project.Id,
                ChangeRequestReferenceText = TrimTo($"{project.CrNumber} | QA Scan", 300)
            };

            var createdBug = await bugService.CreateAsync(bugModel, createdById);
            if (!createdBug.Succeeded)
            {
                bugCreateErrors++;
                _logger.LogWarning(
                    "Unable to create bug from QA scan for project {ProjectId} ({CrNumber}): {Error}",
                    project.Id,
                    project.CrNumber,
                    createdBug.Error);
                continue;
            }

            createdBugs++;
        }

        var summary =
            $"{scanLabel} found {result.Findings.Count} issue(s) for {project.CrNumber}. " +
            $"Created {createdBugs} bug(s) in Bugs module.";
        if (skippedDuplicateBugs > 0)
            summary += $" Skipped {skippedDuplicateBugs} duplicate bug title(s).";
        if (bugCreateErrors > 0)
            summary += $" {bugCreateErrors} bug item(s) failed to save.";

        await NotifyAsync(
            notificationService,
            item.RequestedByUserId,
            $"{scanLabel} completed",
            summary,
            "/Bug/Index");
    }

    private async Task ProcessAutoGenerateAsync(
        ApplicationDbContext db,
        IAiAutomationService automation,
        IRepositoryWorkspaceService workspaceService,
        INotificationService notificationService,
        QaAiAutomationQueueItem item,
        CancellationToken cancellationToken)
    {
        var projectId = item.ProjectId!.Value;
        var project = await db.ChangeRequests
            .Include(c => c.Features)
            .Include(c => c.RepositoryFeatures)
            .FirstOrDefaultAsync(c => c.Id == projectId, cancellationToken);

        if (project == null)
        {
            await NotifyAsync(
                notificationService,
                item.RequestedByUserId,
                "AI test generation failed",
                "QA auto-generate failed because the selected project no longer exists.",
                "/Qa/Index");
            return;
        }

        var createdById = await ResolveCreatedByUserIdAsync(db, item.RequestedByUserId, project.CreatedById, cancellationToken);
        if (string.IsNullOrWhiteSpace(createdById))
            return;

        var workspace = await workspaceService.PrepareReadOnlyAsync(
            project,
            item.RequestedByUserId,
            $"qa-tests-{project.CrNumber}",
            cancellationToken);
        var result = await automation.GenerateTestCasesAsync(
            project,
            workspace.RepositoryDirectory,
            cancellationToken);
        if (!result.Succeeded)
        {
            await NotifyAsync(
                notificationService,
                item.RequestedByUserId,
                "AI test generation failed",
                $"AI test generation failed for {project.CrNumber}: {TrimTo(result.Error, 600)}",
                $"/Qa/Index?projectId={project.Id}");
            return;
        }

        if (result.TestCases.Count == 0)
        {
            await NotifyAsync(
                notificationService,
                item.RequestedByUserId,
                "AI test generation completed",
                $"AI automation analyzed {project.CrNumber} but generated no test cases.",
                $"/Qa/Index?projectId={project.Id}");
            return;
        }

        var testNumbers = await GenerateTestNumbersAsync(db, result.TestCases.Count, cancellationToken);
        for (var i = 0; i < result.TestCases.Count; i++)
        {
            var generated = result.TestCases[i];
            var category = Enum.TryParse<TestCaseCategory>(generated.Category, true, out var parsedCat)
                ? parsedCat
                : TestCaseCategory.Regression;
            var environment = Enum.TryParse<TestCaseEnvironment>(generated.Environment, true, out var parsedEnv)
                ? parsedEnv
                : TestCaseEnvironment.Dev;

            db.TestCases.Add(new TestCase
            {
                TestNumber = testNumbers[i],
                Name = generated.Name,
                Description = generated.Description,
                Module = generated.Module,
                Status = TestCaseStatus.Pending,
                Category = category,
                Environment = environment,
                ChangeRequestId = project.Id,
                CreatedById = createdById,
                IsAutoGenerated = true
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        await NotifyAsync(
            notificationService,
            item.RequestedByUserId,
            "AI test generation completed",
            $"AI automation generated {result.TestCases.Count} module-level test case(s) for {project.CrNumber}.",
            $"/Qa/Index?projectId={project.Id}");
    }

    private async Task ProcessScanModuleAsync(
        ApplicationDbContext db,
        IAiAutomationService automation,
        IRepositoryWorkspaceService workspaceService,
        IBugService bugService,
        INotificationService notificationService,
        QaAiAutomationQueueItem item,
        CancellationToken cancellationToken)
    {
        var testCaseId = item.TestCaseId!.Value;
        var testCase = await db.TestCases
            .Include(t => t.ChangeRequest).ThenInclude(cr => cr!.Features)
            .Include(t => t.ChangeRequest).ThenInclude(cr => cr!.RepositoryFeatures)
            .FirstOrDefaultAsync(t => t.Id == testCaseId, cancellationToken);

        if (testCase == null)
        {
            await NotifyAsync(
                notificationService,
                item.RequestedByUserId,
                "AI scan failed",
                "QA scan failed because the selected test case no longer exists.",
                "/Qa/Index");
            return;
        }

        if (testCase.ChangeRequest == null)
        {
            await NotifyAsync(
                notificationService,
                item.RequestedByUserId,
                "AI scan failed",
                $"Test case {testCase.TestNumber} has no linked project.",
                "/Qa/Index");
            return;
        }

        var createdById = await ResolveCreatedByUserIdAsync(db, item.RequestedByUserId, testCase.CreatedById, cancellationToken);
        if (string.IsNullOrWhiteSpace(createdById))
            return;

        var hasModule = !string.IsNullOrWhiteSpace(testCase.Module);
        var workspace = await workspaceService.PrepareReadOnlyAsync(
            testCase.ChangeRequest,
            item.RequestedByUserId,
            $"qa-module-{testCase.TestNumber}",
            cancellationToken);
        var result = hasModule
            ? await automation.ScanModuleAsync(
                testCase.ChangeRequest,
                testCase.Module!,
                workspace.RepositoryDirectory,
                cancellationToken,
                item.UseSecurityPrompt)
            : await automation.ScanProjectAsync(
                testCase.ChangeRequest,
                workspace.RepositoryDirectory,
                cancellationToken,
                item.UseSecurityPrompt);

        var operationLabel = item.UseSecurityPrompt ? "AI security scan" : "AI scan";

        if (!result.Succeeded)
        {
            await NotifyAsync(
                notificationService,
                item.RequestedByUserId,
                $"{operationLabel} failed",
                $"{operationLabel} failed for {testCase.TestNumber}: {TrimTo(result.Error, 600)}",
                $"/Qa/Index?projectId={testCase.ChangeRequestId}");
            return;
        }

        var scanLabel = hasModule ? $"Module '{testCase.Module}'" : "Project";
        if (result.Findings.Count == 0)
        {
            await NotifyAsync(
                notificationService,
                item.RequestedByUserId,
                $"{operationLabel} completed",
                $"{scanLabel} scan completed for {testCase.TestNumber} with no findings.",
                $"/Qa/Index?projectId={testCase.ChangeRequestId}");
            return;
        }

        var createdBugs = 0;
        var skippedDuplicateBugs = 0;
        var bugCreateErrors = 0;

        var existingBugTitles = await db.BugReports
            .AsNoTracking()
            .Where(b => b.ChangeRequestId == testCase.ChangeRequestId)
            .Select(b => b.Title)
            .ToListAsync(cancellationToken);
        var knownBugTitles = existingBugTitles.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in result.Findings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedTitle = (finding.Title ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedTitle))
                continue;

            if (!knownBugTitles.Add(normalizedTitle))
            {
                skippedDuplicateBugs++;
                continue;
            }

            var moduleImpacted = string.IsNullOrWhiteSpace(finding.ModuleImpacted)
                ? testCase.Module
                : finding.ModuleImpacted;

            var bugModel = new BugFormViewModel
            {
                Title = normalizedTitle,
                Description = TrimTo(
                    $"{finding.Description}\n\nRelated QA Test Case: {testCase.TestNumber}",
                    4000),
                Workflow = TrimTo(finding.Workflow, 4000),
                StepsToReproduce = TrimTo(
                    $"{finding.StepsToReproduce}\n\nRelated QA Test Case: {testCase.TestNumber}",
                    4000),
                ModuleImpacted = TrimTo(moduleImpacted, 200),
                Severity = BugSeverityResolver.Resolve(
                    finding.Severity,
                    finding.Title,
                    finding.Description,
                    finding.Workflow,
                    finding.StepsToReproduce,
                    moduleImpacted),
                Status = BugStatus.New,
                AssigneeType = BugAssigneeType.Developer,
                ChangeRequestId = testCase.ChangeRequestId,
                ChangeRequestReferenceText = TrimTo(
                    $"{testCase.ChangeRequest.CrNumber} | QA Test Case: {testCase.TestNumber}",
                    300)
            };

            var createdBug = await bugService.CreateAsync(bugModel, createdById);
            if (!createdBug.Succeeded)
            {
                bugCreateErrors++;
                _logger.LogWarning(
                    "Unable to create bug from QA module scan for test case {TestCaseNumber}: {Error}",
                    testCase.TestNumber,
                    createdBug.Error);
                continue;
            }

            createdBugs++;
        }

        var summary =
            $"{scanLabel} scan for {testCase.TestNumber} found {result.Findings.Count} issue(s). " +
            $"Created {createdBugs} bug(s) in Bugs module.";
        if (skippedDuplicateBugs > 0)
            summary += $" Skipped {skippedDuplicateBugs} duplicate bug title(s).";
        if (bugCreateErrors > 0)
            summary += $" {bugCreateErrors} bug item(s) failed to save.";

        await NotifyAsync(
            notificationService,
            item.RequestedByUserId,
            $"{operationLabel} completed",
            summary,
            "/Bug/Index");
    }

    private static async Task<string?> ResolveCreatedByUserIdAsync(
        ApplicationDbContext db,
        string preferredUserId,
        string? fallbackUserId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(preferredUserId))
        {
            var preferredExists = await db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == preferredUserId, cancellationToken);
            if (preferredExists)
                return preferredUserId;
        }

        if (!string.IsNullOrWhiteSpace(fallbackUserId))
        {
            var fallbackExists = await db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == fallbackUserId, cancellationToken);
            if (fallbackExists)
                return fallbackUserId;
        }

        return null;
    }

    private static async Task<List<string>> GenerateTestNumbersAsync(
        ApplicationDbContext db,
        int count,
        CancellationToken cancellationToken)
    {
        if (count <= 0)
            return [];

        var year = DateTime.UtcNow.Year;
        var prefix = $"TC-{year}-";
        var existingNumbers = await db.TestCases
            .AsNoTracking()
            .Where(t => t.TestNumber.StartsWith(prefix))
            .Select(t => t.TestNumber)
            .ToListAsync(cancellationToken);

        var maxSeq = existingNumbers
            .Select(ParseTestNumberSequence)
            .DefaultIfEmpty(0)
            .Max();

        var numbers = new List<string>(count);
        for (var i = 1; i <= count; i++)
            numbers.Add($"{prefix}{maxSeq + i:D4}");

        return numbers;
    }

    private static int ParseTestNumberSequence(string? testNumber)
    {
        if (string.IsNullOrWhiteSpace(testNumber))
            return 0;

        var parts = testNumber.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3 && int.TryParse(parts[2], out var seq) ? seq : 0;
    }

    private async Task TryNotifyAsync(string userId, string title, string message, string? linkUrl)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await NotifyAsync(notificationService, userId, title, message, linkUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed sending QA automation notification to user {UserId}.",
                userId);
        }
    }

    private static async Task NotifyAsync(
        INotificationService notificationService,
        string userId,
        string title,
        string message,
        string? linkUrl)
    {
        await notificationService.CreateAsync(
            userId,
            TrimTo(title, 200),
            TrimTo(message, 1000),
            linkUrl);
    }

    private static string TrimTo(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length <= maxLength)
            return text;
        return text[..maxLength].Trim();
    }
}
