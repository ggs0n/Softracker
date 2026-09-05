using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AiAccountController(
    IAiAccountService account) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Status(
        CancellationToken cancellationToken)
    {
        try
        {
            return Json(await account.GetStatusAsync(cancellationToken));
        }
        catch (Exception)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new
                {
                    state = "unavailable",
                    message = "The local AI automation service is unavailable."
                });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Connect(
        CancellationToken cancellationToken)
    {
        try
        {
            return Json(await account.StartLoginAsync(cancellationToken));
        }
        catch (Exception)
        {
            return Problem(
                title: "ChatGPT connection could not be started.",
                detail: "Confirm AutomationService and codex app-server are running.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet]
    public async Task<IActionResult> LoginStatus(
        string id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "A login identifier is required." });

        return Json(await account.GetLoginStatusAsync(
            id,
            cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(
        string id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "A login identifier is required." });

        await account.CancelLoginAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken)
    {
        await account.LogoutAsync(cancellationToken);
        return NoContent();
    }
}
