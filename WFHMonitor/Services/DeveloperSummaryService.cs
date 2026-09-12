using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public class DeveloperSummaryService : IDeveloperSummaryService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IGitHubService _gitHubService;

    public DeveloperSummaryService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IGitHubService gitHubService)
    {
        _db = db;
        _userManager = userManager;
        _gitHubService = gitHubService;
    }

    public async Task<DeveloperSummaryViewModel> BuildSummaryAsync(string userId)
    {
        var currentUser = await _userManager.FindByIdAsync(userId);
        var developerEmail = (currentUser?.Email ?? string.Empty).Trim();

        var bugs = await _db.BugReports
            .Include(b => b.ChangeRequest)
            .Where(b => b.AssigneeType == BugAssigneeType.Developer && b.AssignedDeveloperId == userId)
            .OrderByDescending(b => b.UpdatedAt)
            .AsNoTracking()
            .ToListAsync();

        var changeRequests = await _db.ChangeRequests
            .Include(c => c.Pics.Where(p => p.EmployeeId == userId))
            .Where(c => c.Pics.Any(p => p.EmployeeId == userId))
            .OrderByDescending(c => c.UpdatedAt)
            .AsNoTracking()
            .ToListAsync();

        var tasks = await _db.WorkTasks
            .Where(t => t.AssigneeId == userId)
            .AsNoTracking()
            .ToListAsync();

        var bugIds = bugs.Select(b => b.Id).ToList();
        var reopenedBugPenaltyCount = bugIds.Count == 0
            ? 0
            : await _db.BugActivities
                .Where(a =>
                    bugIds.Contains(a.BugReportId) &&
                    a.OldStatus == BugStatus.Complete &&
                    a.NewStatus != BugStatus.Complete &&
                    a.NewAssignedDeveloperId == userId)
                .CountAsync();

        var completedBugs = bugs.Where(b => b.Status == BugStatus.Complete).ToList();
        var criticalBugFixedCount = completedBugs.Count(b => b.ChangeRequest?.Priority == CrPriority.Critical);
        var bugFixedCount = completedBugs.Count - criticalBugFixedCount;
        var failedSlaPenaltyCount = completedBugs.Count(b =>
            b.ChangeRequest?.TimelineEnd.HasValue == true &&
            b.UpdatedAt.Date > b.ChangeRequest.TimelineEnd!.Value.Date);
        var crDeliveredOnTimeCount = changeRequests.Count(c =>
            c.Status == CrStatus.Done &&
            c.TimelineEnd.HasValue &&
            c.UpdatedAt.Date <= c.TimelineEnd.Value.Date);
        var taskCompletedBeforeDueDateCount = tasks.Count(t =>
            t.Status == WorkTaskStatus.Done &&
            t.DueDate.HasValue &&
            t.UpdatedAt.Date < t.DueDate.Value.Date);

        var vm = new DeveloperSummaryViewModel
        {
            AssignedBugCount = bugs.Count,
            AssignedChangeRequestCount = changeRequests.Count,
            DeveloperEmail = developerEmail,
            AssignedBugs = bugs,
            AssignedChangeRequests = changeRequests,
            BugFixedCount = bugFixedCount,
            CriticalBugFixedCount = criticalBugFixedCount,
            CrDeliveredOnTimeCount = crDeliveredOnTimeCount,
            TaskCompletedBeforeDueDateCount = taskCompletedBeforeDueDateCount,
            ReopenedBugPenaltyCount = reopenedBugPenaltyCount,
            FailedSlaPenaltyCount = failedSlaPenaltyCount
        };

        var githubLinkedCrs = changeRequests
            .Where(c => HasGitHubConfig(c, out _, out _, out _))
            .ToList();

        vm.Branches = githubLinkedCrs
            .Select(c =>
            {
                HasGitHubConfig(c, out var owner, out var repo, out var branch);
                return new DeveloperGitHubBranchItem
                {
                    CrNumber = c.CrNumber,
                    Repo = $"{owner}/{repo}",
                    Branch = branch!
                };
            })
            .ToList();
        vm.LinkedBranchCount = vm.Branches.Count;

        if (!string.IsNullOrWhiteSpace(developerEmail))
        {
            try
            {
                var semaphore = new SemaphoreSlim(5, 5);
                var commitTasks = githubLinkedCrs.Select(async cr =>
                {
                    HasGitHubConfig(cr, out var owner, out var repo, out var branch);
                    await semaphore.WaitAsync();
                    try
                    {
                        var commits = await _gitHubService.GetCommitsAsync(
                            owner!, repo!, branch!, count: 15);
                        return commits
                            .Where(c => string.Equals(c.AuthorEmail, developerEmail, StringComparison.OrdinalIgnoreCase))
                            .Select(commit => new DeveloperGitHubCommitItem
                            {
                                CrNumber = cr.CrNumber,
                                Repo = $"{owner}/{repo}",
                                Branch = branch!,
                                Sha = commit.Sha,
                                Message = commit.Message,
                                Date = commit.Date,
                                Url = commit.Url
                            });
                    }
                    finally { semaphore.Release(); }
                });
                var results = await Task.WhenAll(commitTasks);
                var commitItems = results.SelectMany(x => x).ToList();

                vm.RecentCommits = commitItems
                    .OrderByDescending(c => c.Date)
                    .Take(20)
                    .ToList();
                vm.RecentCommitCount = vm.RecentCommits.Count;
            }
            catch (Exception ex)
            {
                vm.GitHubError = ex.Message;
            }
        }

        return vm;
    }

    private static bool HasGitHubConfig(ChangeRequest project, out string? owner, out string? repo, out string? branch)
    {
        owner = project.GitHubRepoOwner?.Trim();
        repo = project.GitHubRepoName?.Trim();
        branch = project.GitHubBranch?.Trim();

        if ((string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo)) &&
            !string.IsNullOrWhiteSpace(project.GitHubRepoUrl) &&
            GitHubRepositoryUrlParser.TryParse(project.GitHubRepoUrl!, out var parsedOwner, out var parsedRepo, out var parsedBranch))
        {
            owner = parsedOwner;
            repo = parsedRepo;
            if (string.IsNullOrWhiteSpace(branch))
                branch = parsedBranch;
        }

        return !string.IsNullOrWhiteSpace(owner) &&
               !string.IsNullOrWhiteSpace(repo) &&
               !string.IsNullOrWhiteSpace(branch);
    }

}
