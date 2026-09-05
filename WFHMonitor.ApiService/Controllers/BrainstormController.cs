using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize]
public sealed class BrainstormController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    IBrainstormDesignGenerator generator,
    IBrainstormResultImporter resultImporter,
    IAiAutomationService aiAutomation) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var designs = await db.BrainstormDesignProjects
            .AsNoTracking()
            .Where(project => project.CreatedById == userId)
            .OrderByDescending(project => project.CreatedAt)
            .Select(project => new BrainstormDesignProjectSummary(
                project.Id,
                project.Title,
                project.Summary,
                project.Technology,
                project.CloudHostingTarget,
                project.UserCount,
                project.CreatedAt,
                project.SourceMode))
            .ToListAsync(cancellationToken);

        return Json(designs);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int id,
        CancellationToken cancellationToken)
    {
        var project = await FindOwnedProjectAsync(id, cancellationToken);
        if (project is null)
            return NotFound(new { message = "Brainstorm blueprint not found." });

        var details = BrainstormDesignProjectDetails.From(project);
        return Json(details with
        {
            Blueprint = BrainstormBlueprintConsistency.Apply(
                details.Blueprint,
                project.Features)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Generate(
        [FromBody] BrainstormGenerateDesignRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "A design brief is required." });

        var trimmed = request.Trimmed();
        var errors = BrainstormRequestValidator.Validate(trimmed);
        if (errors.Count > 0)
            return BadRequest(new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The design brief is invalid."
            });

        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        var blueprint = await generator.GenerateAsync(
            trimmed,
            cancellationToken);
        var project = BrainstormDesignProject.From(
            trimmed,
            blueprint,
            userId);
        db.BrainstormDesignProjects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        return Json(BrainstormDesignProjectDetails.From(project));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GenerateModuleImages(
        int id,
        CancellationToken cancellationToken)
    {
        var project = await FindOwnedProjectAsync(id, cancellationToken);
        if (project is null)
            return NotFound(new { message = "Brainstorm blueprint not found." });

        var details = BrainstormDesignProjectDetails.From(project);
        var blueprint = BrainstormBlueprintConsistency.Apply(
            details.Blueprint,
            project.Features);
        var modules = BuildModuleImageContexts(
            blueprint,
            project.Features);
        if (modules.Count == 0)
        {
            return BadRequest(
                new { message = "No main module is available for image generation." });
        }

        var result = await aiAutomation.GenerateModuleImagesAsync(
            new(
                blueprint.Title,
                project.Summary,
                project.Technology,
                modules),
            cancellationToken);
        if (!result.Succeeded)
        {
            return Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status502BadGateway,
                title: "Module images could not be generated.");
        }

        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportChatGpt(
        [FromBody] BrainstormImportChatGptResultRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { message = "An imported result is required." });

        var errors = BrainstormRequestValidator.Validate(request);
        if (errors.Count > 0)
            return BadRequest(new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The imported result is invalid."
            });

        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return Forbid();

        try
        {
            var trimmed = request.ToGenerateRequest();
            var blueprint = BrainstormBlueprintConsistency.Apply(
                resultImporter.Import(request.ChatGptResult),
                trimmed);
            var project = BrainstormDesignProject.From(
                trimmed,
                blueprint,
                userId);
            db.BrainstormDesignProjects.Add(project);
            await db.SaveChangesAsync(cancellationToken);
            return Json(BrainstormDesignProjectDetails.From(project));
        }
        catch (Exception exception)
            when (exception is InvalidOperationException
                  or System.Text.Json.JsonException
                  or NotSupportedException)
        {
            return Problem(
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The imported blueprint is invalid.");
        }
    }

    [HttpDelete]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var project = await FindOwnedProjectAsync(id, cancellationToken);
        if (project is null)
            return NotFound(new { message = "Brainstorm blueprint not found." });

        db.BrainstormDesignProjects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<BrainstormDesignProject?> FindOwnedProjectAsync(
        int id,
        CancellationToken cancellationToken)
    {
        var userId = userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return await db.BrainstormDesignProjects
            .FirstOrDefaultAsync(
                project => project.Id == id
                           && project.CreatedById == userId,
                cancellationToken);
    }

    private static IReadOnlyList<AiModuleImageContext>
        BuildModuleImageContexts(
            BrainstormDesignBlueprint blueprint,
            string features) =>
        BrainstormBlueprintConsistency.ExtractCapabilities(features)
            .Take(10)
            .Select(capability =>
            {
                var service = blueprint.ServiceDetails?
                    .FirstOrDefault(item => Related(
                        capability,
                        item.Name,
                        item.Responsibility,
                        item.DataOwnership));
                var deployment = blueprint.DeploymentServices?
                    .FirstOrDefault(item => Related(
                        capability,
                        item.Module,
                        item.Reason));
                var interfaces = new List<string>();
                interfaces.AddRange(
                    (blueprint.RestEndpoints ?? [])
                    .Where(item => Related(
                        capability,
                        item.Service,
                        item.Path,
                        item.Purpose))
                    .Select(item => $"{item.Method} {item.Path}"));
                interfaces.AddRange(
                    (blueprint.GrpcContracts ?? [])
                    .Where(item => Related(
                        capability,
                        item.Service,
                        item.Purpose))
                    .Select(item =>
                        $"gRPC {item.Contract}.{item.RpcMethod}"));
                var ownedData = (blueprint.TableSchemas ?? [])
                    .Where(item => Related(
                        capability,
                        item.Name,
                        item.Purpose))
                    .Select(item => item.Name)
                    .ToArray();
                var jobs = (blueprint.BatchJobs ?? [])
                    .Where(item => Related(
                        capability,
                        item.Name,
                        item.OwnerService,
                        item.Responsibility))
                    .Select(item => $"{item.Name} ({item.Trigger})")
                    .ToArray();
                return new AiModuleImageContext(
                    capability,
                    service?.Responsibility
                        ?? $"Implements the complete {capability} workflow.",
                    service?.Runtime
                        ?? deployment?.Runtime
                        ?? "Application service",
                    service?.Deployment
                        ?? deployment?.RecommendedService
                        ?? "Configured application runtime",
                    interfaces.Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(8)
                        .ToArray(),
                    ownedData.Take(6).ToArray(),
                    jobs.Take(6).ToArray());
            })
            .ToArray();

    private static bool Related(
        string capability,
        params string[] values) =>
        values.Any(value => value?.Contains(
            capability,
            StringComparison.OrdinalIgnoreCase) == true);
}
