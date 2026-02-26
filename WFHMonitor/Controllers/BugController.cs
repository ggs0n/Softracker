using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin,Tester,Developer")]
public class BugController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBugService _bugService;

    public BugController(UserManager<ApplicationUser> userManager, IBugService bugService)
    {
        _userManager = userManager;
        _bugService = bugService;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var userId = _userManager.GetUserId(User);
            var bugs = await _bugService.GetIndexBugsAsync(User.IsInRole("Developer"), userId);
            return View(bugs);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return View(new List<BugReport>());
        }
    }

    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var bug = await _bugService.GetDetailsAsync(id);
            if (bug == null) return NotFound();

            if (User.IsInRole("Developer"))
            {
                var userId = _userManager.GetUserId(User);
                if (bug.AssignedDeveloperId != userId)
                    return Forbid();
            }

            return View(bug);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> Create()
    {
        var vm = new BugFormViewModel();
        await _bugService.PopulateFormOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> Create(BugFormViewModel model)
    {
        if (!ModelState.IsValid)
            return await ReturnBugFormWithOptions(model);

        try
        {
            var userId = _userManager.GetUserId(User)!;
            var result = await _bugService.CreateAsync(model, userId);
            if (!result.Succeeded)
                return await ReturnBugFormWithOptions(model, result.Error);

            TempData["Success"] = "Bug created.";
            return RedirectToAction(nameof(Details), new { id = result.BugId });
        }
        catch (Exception ex)
        {
            return await ReturnBugFormWithOptions(model, ex.Message);
        }
    }

    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _bugService.BuildEditViewModelAsync(id);
        if (vm == null) return NotFound();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> Edit(int id, BugFormViewModel model)
    {
        if (!ModelState.IsValid)
            return await ReturnBugFormWithOptions(model);

        try
        {
            var result = await _bugService.UpdateAsync(id, model);
            if (!result.Succeeded)
                return await ReturnBugFormWithOptions(model, result.Error);

            TempData["Success"] = "Bug updated.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            return await ReturnBugFormWithOptions(model, ex.Message);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester,Developer")]
    public async Task<IActionResult> UpdateStatus(int id, BugStatus status)
    {
        var bug = await _bugService.GetByIdAsync(id);
        if (bug == null) return NotFound();

        var userId = _userManager.GetUserId(User);
        if (!CanUpdateStatus(bug, status, userId))
            return Forbid();

        await _bugService.UpdateStatusAsync(bug, status);

        TempData["Success"] = "Status updated.";
        return RedirectToAction(nameof(Details), new { id = bug.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> UploadScreenshot(int id, IFormFile file, string? returnUrl)
    {
        return await HandleUploadResult(
            _bugService.UploadScreenshotAsync(id, file),
            "Screenshot uploaded.",
            returnUrl,
            id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> DeleteScreenshot(int screenshotId, int bugId)
    {
        var result = await _bugService.DeleteScreenshotAsync(screenshotId);
        if (!result.Succeeded) return NotFound();
        TempData["Success"] = "Screenshot deleted.";
        return RedirectToAction(nameof(Details), new { id = bugId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> UploadDocument(int id, IFormFile file, string? returnUrl)
    {
        return await HandleUploadResult(
            _bugService.UploadDocumentAsync(id, file),
            "Document uploaded.",
            returnUrl,
            id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> DeleteDocument(int documentId, int bugId)
    {
        var result = await _bugService.DeleteDocumentAsync(documentId);
        if (!result.Succeeded) return NotFound();
        TempData["Success"] = "Document deleted.";
        return RedirectToAction(nameof(Details), new { id = bugId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Tester")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _bugService.DeleteBugAsync(id);
        if (!result.Succeeded) return NotFound();
        TempData["Success"] = "Bug deleted.";
        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectToLocal(string? returnUrl, int id)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Details), new { id });
    }

    private bool CanUpdateStatus(BugReport bug, BugStatus status, string? userId)
    {
        var isDeveloper = User.IsInRole("Developer");
        var isAdminOrTester = User.IsInRole("Admin") || User.IsInRole("Tester");

        if (isDeveloper)
        {
            if (bug.AssignedDeveloperId != userId || bug.AssigneeType == BugAssigneeType.Agent)
                return false;
        }

        if (!isAdminOrTester && status is not BugStatus.New and not BugStatus.Testing)
            return false;

        return true;
    }

    private async Task<IActionResult> ReturnBugFormWithOptions(BugFormViewModel model, string? error = null)
    {
        if (!string.IsNullOrWhiteSpace(error))
            TempData["Error"] = error;

        await _bugService.PopulateFormOptionsAsync(model);
        return View(model);
    }

    private async Task<IActionResult> HandleUploadResult(
        Task<(bool Succeeded, string Error)> operation,
        string successMessage,
        string? returnUrl,
        int bugId)
    {
        var result = await operation;
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? successMessage : result.Error;
        return RedirectToLocal(returnUrl, bugId);
    }
}
