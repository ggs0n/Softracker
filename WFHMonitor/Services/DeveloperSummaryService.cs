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

        var vm = new DeveloperSummaryViewModel
        {
            AssignedBugCount = bugs.Count,
            AssignedChangeRequestCount = changeRequests.Count,
            DeveloperEmail = developerEmail,
            AssignedBugs = bugs,
            AssignedChangeRequests = changeRequests
        };

        var githubLinkedCrs = changeRequests
            .Where(c => !string.IsNullOrWhiteSpace(c.GitHubRepoOwner)
                        && !string.IsNullOrWhiteSpace(c.GitHubRepoName)
                        && !string.IsNullOrWhiteSpace(c.GitHubBranch))
            .ToList();

        vm.Branches = githubLinkedCrs
            .Select(c => new DeveloperGitHubBranchItem
            {
                CrNumber = c.CrNumber,
                Repo = $"{c.GitHubRepoOwner}/{c.GitHubRepoName}",
                Branch = c.GitHubBranch!
            })
            .ToList();
        vm.LinkedBranchCount = vm.Branches.Count;

        if (!string.IsNullOrWhiteSpace(developerEmail))
        {
            try
            {
                var commitItems = new List<DeveloperGitHubCommitItem>();

                foreach (var cr in githubLinkedCrs)
                {
                    var commits = await _gitHubService.GetCommitsAsync(
                        cr.GitHubRepoOwner!, cr.GitHubRepoName!, cr.GitHubBranch!, count: 15);

                    foreach (var commit in commits.Where(c =>
                                 string.Equals(c.AuthorEmail, developerEmail, StringComparison.OrdinalIgnoreCase)))
                    {
                        commitItems.Add(new DeveloperGitHubCommitItem
                        {
                            CrNumber = cr.CrNumber,
                            Repo = $"{cr.GitHubRepoOwner}/{cr.GitHubRepoName}",
                            Branch = cr.GitHubBranch!,
                            Sha = commit.Sha,
                            Message = commit.Message,
                            Date = commit.Date,
                            Url = commit.Url
                        });
                    }
                }

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
}
