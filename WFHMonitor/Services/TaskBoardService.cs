using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Services;

public class TaskBoardService : ITaskBoardService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public TaskBoardService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<TaskBoardViewModel> BuildBoardAsync(bool isAdmin, string? userId)
    {
        IQueryable<WorkTask> query = _db.WorkTasks
            .Include(t => t.Assignee)
            .Include(t => t.ChangeRequest)
            .Include(t => t.BugReport)
            .AsNoTracking();

        if (!isAdmin)
            query = query.Where(t => t.AssigneeId == userId);

        var tasks = await query.OrderBy(t => t.DueDate).ToListAsync();

        return new TaskBoardViewModel
        {
            ToDo = tasks.Where(t => t.Status == WorkTaskStatus.ToDo).ToList(),
            InProgress = tasks.Where(t => t.Status == WorkTaskStatus.InProgress).ToList(),
            Blocked = tasks.Where(t => t.Status == WorkTaskStatus.Blocked).ToList(),
            Done = tasks.Where(t => t.Status == WorkTaskStatus.Done).ToList()
        };
    }

    public async Task<List<SelectListItem>> GetAssigneeOptionsAsync()
    {
        var roles = new[] { "Employee", "Developer" };
        var usersByRole = new List<IList<ApplicationUser>>();
        foreach (var role in roles)
            usersByRole.Add(await _userManager.GetUsersInRoleAsync(role));

        var users = usersByRole
            .SelectMany(x => x)
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .ToList();

        return users
            .OrderBy(u => u.FullName)
            .Select(u => new SelectListItem(u.FullName, u.Id))
            .ToList();
    }

    public Task<List<SelectListItem>> GetChangeRequestOptionsAsync()
    {
        return _db.ChangeRequests
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new SelectListItem($"{c.CrNumber} - {c.Title}", c.Id.ToString()))
            .ToListAsync();
    }

    public Task<List<SelectListItem>> GetBugOptionsAsync()
    {
        return _db.BugReports
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new SelectListItem($"{b.BugNumber} - {b.Title}", b.Id.ToString()))
            .ToListAsync();
    }

    public Task<WorkTask?> GetByIdAsync(int id)
    {
        return _db.WorkTasks.FindAsync(id).AsTask();
    }

    public async Task CreateAsync(TaskCreateViewModel model, string createdById)
    {
        var task = new WorkTask
        {
            Title = model.Title,
            Details = model.Details,
            Description = model.Description,
            AssigneeId = model.AssigneeId,
            Status = model.Status,
            TimelineStart = model.TimelineStart,
            TimelineEnd = model.TimelineEnd,
            DueDate = model.DueDate,
            ChangeRequestId = model.ChangeRequestId,
            BugReportId = model.BugReportId,
            CreatedById = createdById
        };

        _db.WorkTasks.Add(task);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(WorkTask task, TaskEditViewModel model, bool isAdmin)
    {
        task.Title = model.Title;
        task.Details = model.Details;
        task.Description = model.Description;
        task.Status = model.Status;
        task.TimelineStart = model.TimelineStart;
        task.TimelineEnd = model.TimelineEnd;
        task.DueDate = model.DueDate;
        task.ChangeRequestId = model.ChangeRequestId;
        task.BugReportId = model.BugReportId;
        task.UpdatedAt = DateTime.UtcNow;

        if (isAdmin)
            task.AssigneeId = model.AssigneeId;

        await _db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var task = await _db.WorkTasks.FindAsync(id);
        if (task == null) return false;

        _db.WorkTasks.Remove(task);
        await _db.SaveChangesAsync();
        return true;
    }
}
