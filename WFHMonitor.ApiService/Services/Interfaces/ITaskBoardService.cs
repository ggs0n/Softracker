using Microsoft.AspNetCore.Mvc.Rendering;
using WFHMonitor.Models;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services.Interfaces;

public interface ITaskBoardService
{
    Task<TaskBoardViewModel> BuildBoardAsync(bool isAdmin, string? userId);
    Task<List<SelectListItem>> GetAssigneeOptionsAsync();
    Task<List<SelectListItem>> GetChangeRequestOptionsAsync();
    Task<List<SelectListItem>> GetBugOptionsAsync();
    Task<WorkTask?> GetByIdAsync(int id);
    Task CreateAsync(TaskCreateViewModel model, string createdById);
    Task UpdateAsync(WorkTask task, TaskEditViewModel model, bool isAdmin);
    Task<bool> DeleteAsync(int id);
}
