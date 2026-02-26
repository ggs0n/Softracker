using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize]
public class TaskBoardController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITaskBoardService _taskBoardService;
    private bool IsAdmin => User.IsInRole("Admin");
    private string? CurrentUserId => _userManager.GetUserId(User);

    public TaskBoardController(
        UserManager<ApplicationUser> userManager,
        ITaskBoardService taskBoardService)
    {
        _userManager = userManager;
        _taskBoardService = taskBoardService;
    }

    public async Task<IActionResult> Index()
    {
        var vm = await _taskBoardService.BuildBoardAsync(IsAdmin, CurrentUserId);
        return View(vm);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create()
    {
        await PopulateLookupsAsync();
        return View(new TaskCreateViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(TaskCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync();
            return View(model);
        }

        await _taskBoardService.CreateAsync(model, CurrentUserId!);
        TempData["Success"] = "Task created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var task = await _taskBoardService.GetByIdAsync(id);
        if (task == null) return NotFound();

        if (!CanAccessTask(task))
            return Forbid();

        await PopulateLookupsAsync();
        var vm = new TaskEditViewModel
        {
            Id = task.Id,
            Title = task.Title,
            Details = task.Details,
            Description = task.Description,
            AssigneeId = task.AssigneeId,
            Status = task.Status,
            TimelineStart = task.TimelineStart,
            TimelineEnd = task.TimelineEnd,
            DueDate = task.DueDate,
            ChangeRequestId = task.ChangeRequestId,
            BugReportId = task.BugReportId
        };
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TaskEditViewModel model)
    {
        if (id != model.Id) return BadRequest();

        var task = await _taskBoardService.GetByIdAsync(id);
        if (task == null) return NotFound();

        if (!CanAccessTask(task))
            return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync();
            return View(model);
        }

        await _taskBoardService.UpdateAsync(task, model, IsAdmin);
        TempData["Success"] = "Task updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await _taskBoardService.DeleteAsync(id))
            return NotFound();
        TempData["Success"] = "Task deleted.";
        return RedirectToAction(nameof(Index));
    }

    private bool CanAccessTask(WorkTask task) => IsAdmin || task.AssigneeId == CurrentUserId;

    private async Task PopulateLookupsAsync()
    {
        ViewBag.Assignees = await _taskBoardService.GetAssigneeOptionsAsync();
        ViewBag.ChangeRequests = await _taskBoardService.GetChangeRequestOptionsAsync();
        ViewBag.Bugs = await _taskBoardService.GetBugOptionsAsync();
    }
}
