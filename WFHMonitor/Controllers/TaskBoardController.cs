using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WFHMonitor.Controllers;

[Authorize]
public class TaskBoardController : Controller
{
    public IActionResult Index()
    {
        return TaskBoardRemoved();
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        return TaskBoardRemoved();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public IActionResult Create(object model)
    {
        return TaskBoardRemoved();
    }

    public IActionResult Edit(int id)
    {
        return TaskBoardRemoved();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Edit(int id, object model)
    {
        return TaskBoardRemoved();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public IActionResult Delete(int id)
    {
        return TaskBoardRemoved();
    }

    private IActionResult TaskBoardRemoved()
    {
        TempData["Info"] = "Task Board module is removed.";
        return RedirectToAction("Index", "ChangeRequest");
    }
}
