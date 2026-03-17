using Microsoft.AspNetCore.Http;
using WFHMonitor.Models;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface IBugService
{
    Task<List<BugReport>> GetIndexBugsAsync(bool isAdmin, int? orgTeamId);
    Task<BugReport?> GetDetailsAsync(int id);
    Task<BugReport?> GetByIdAsync(int id);
    Task PopulateFormOptionsAsync(BugFormViewModel model, bool isAdmin, int? orgTeamId);
    Task<BugFormViewModel?> BuildEditViewModelAsync(int id, bool isAdmin, int? orgTeamId);
    Task<(bool Succeeded, string Error, int BugId)> CreateAsync(BugFormViewModel model, string createdById);
    Task<(bool Succeeded, string Error)> UpdateAsync(int id, BugFormViewModel model);
    Task<(bool Succeeded, string Error)> UpdatePullRequestUrlAsync(int id, string? pullRequestUrl);
    Task UpdateStatusAsync(BugReport bug, BugStatus status);
    Task<(bool Succeeded, string Error)> UploadScreenshotAsync(int id, IFormFile file);
    Task<(bool Succeeded, string Error)> DeleteScreenshotAsync(int screenshotId);
    Task<(bool Succeeded, string Error)> UploadDocumentAsync(int id, IFormFile file);
    Task<(bool Succeeded, string Error)> DeleteDocumentAsync(int documentId);
    Task<(bool Succeeded, string Error)> DeleteBugAsync(int id);
}
