using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WFHMonitor.Data;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Controllers;

[Authorize]
public class ProjectKickStartController : Controller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApplicationDbContext _db;
    private readonly ICodexBugScanService _codexService;
    private readonly ICodexAuthService _codexAuthService;

    public ProjectKickStartController(
        ApplicationDbContext db,
        ICodexBugScanService codexService,
        ICodexAuthService codexAuthService)
    {
        _db = db;
        _codexService = codexService;
        _codexAuthService = codexAuthService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var history = await _db.ProjectKickStartDesigns
            .AsNoTracking()
            .Where(item => item.CreatedById == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(30)
            .ToListAsync(cancellationToken);

        var selected = id.HasValue
            ? history.FirstOrDefault(item => item.Id == id.Value)
            : history.FirstOrDefault();

        var page = BuildPage(selected, history);
        await PopulateCodexStatusAsync(page, cancellationToken);
        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate([Bind(Prefix = "Input")] ProjectKickStartInputViewModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var history = await LoadHistory(userId, cancellationToken);
            var invalidPage = new ProjectKickStartPageViewModel { Input = input, History = history };
            await PopulateCodexStatusAsync(invalidPage, cancellationToken);
            return View("Index", invalidPage);
        }

        var authStatus = await _codexAuthService.GetStatusAsync(cancellationToken);
        if (!authStatus.IsAuthenticated)
        {
            ModelState.AddModelError(string.Empty, "Connect Codex OAuth before generating an AI blueprint.");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var disconnectedPage = new ProjectKickStartPageViewModel
            {
                Input = input,
                History = await LoadHistory(userId, cancellationToken),
                IsCodexAvailable = authStatus.IsAvailable,
                IsCodexConnected = false,
                IsCodexChatGptLogin = authStatus.IsChatGptLogin,
                CodexStatusMessage = authStatus.Message
            };
            return View("Index", disconnectedPage);
        }

        var generation = await _codexService.GenerateProjectKickStartAsync(input, cancellationToken: cancellationToken);
        if (!generation.Succeeded || generation.Blueprint is null)
        {
            ModelState.AddModelError(string.Empty, generation.Error);
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var failedPage = new ProjectKickStartPageViewModel
            {
                Input = input,
                History = await LoadHistory(userId, cancellationToken),
                IsCodexAvailable = authStatus.IsAvailable,
                IsCodexConnected = authStatus.IsAuthenticated,
                IsCodexChatGptLogin = authStatus.IsChatGptLogin,
                CodexStatusMessage = generation.Error
            };
            return View("Index", failedPage);
        }

        var blueprint = generation.Blueprint;
        var design = new ProjectKickStartDesign
        {
            Title = blueprint.Title,
            Summary = input.Summary.Trim(),
            Technology = input.Technology.Trim(),
            CloudHostingTarget = string.IsNullOrWhiteSpace(input.CloudHostingTarget) ? null : input.CloudHostingTarget.Trim(),
            UserCount = input.UserCount.Trim(),
            Features = input.Features.Trim(),
            UiDirection = string.IsNullOrWhiteSpace(input.UiDirection) ? null : input.UiDirection.Trim(),
            BlueprintJson = JsonSerializer.Serialize(blueprint, JsonOptions),
            SourceMode = blueprint.SourceMode,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier)!
        };

        _db.ProjectKickStartDesigns.Add(design);
        await _db.SaveChangesAsync(cancellationToken);

        if (authStatus.IsChatGptLogin)
        {
            var pageGeneration = blueprint.VisualPlan?.PageSamples.Count > 0
                ? await GenerateAndAttachImagesAsync(design, blueprint, 2, cancellationToken)
                : new CodexProjectImageGenerationResult(false, "The blueprint has no page image specifications.", []);
            var currentBlueprint = DeserializeBlueprint(design.BlueprintJson) ?? blueprint;
            var architectureGeneration = await GenerateAndAttachArchitectureAsync(design, currentBlueprint, cancellationToken);

            TempData["Success"] = architectureGeneration.Succeeded
                ? $"Project blueprint generated with {pageGeneration.Images.Count} page image(s) and one architecture diagram."
                : $"Project blueprint generated with {pageGeneration.Images.Count} page image(s).";
            if (!pageGeneration.Succeeded || !architectureGeneration.Succeeded)
                TempData["Error"] = string.Join(" ", new[] { pageGeneration.Error, architectureGeneration.Error }.Where(error => !string.IsNullOrWhiteSpace(error)));
        }
        else
        {
            TempData["Success"] = "Project blueprint generated and saved. Use the image-sample button after connecting with Sign in with ChatGPT to generate previews.";
        }

        return RedirectToAction(nameof(Index), new { id = design.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateImages(int id, int count = 2, CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var design = await _db.ProjectKickStartDesigns
            .FirstOrDefaultAsync(item => item.Id == id && item.CreatedById == userId, cancellationToken);
        if (design is null)
            return NotFound();

        var blueprint = DeserializeBlueprint(design.BlueprintJson);
        if (blueprint?.VisualPlan is null || blueprint.VisualPlan.PageSamples.Count == 0)
        {
            TempData["Error"] = "This saved blueprint has no page image specifications. Generate a new blueprint first.";
            return RedirectToAction(nameof(Index), new { id });
        }

        var authStatus = await _codexAuthService.GetStatusAsync(cancellationToken);
        if (!authStatus.IsAuthenticated || !authStatus.IsChatGptLogin)
        {
            TempData["Error"] = "Connect Softracker using Sign in with ChatGPT. API-key authentication is not accepted for image generation.";
            return RedirectToAction(nameof(Index), new { id });
        }

        var requestedCount = count >= 5 ? 5 : 2;
        var generation = await GenerateAndAttachImagesAsync(design, blueprint, requestedCount, cancellationToken);

        if (generation.Succeeded)
            TempData["Success"] = $"Generated {generation.Images.Count} real page images using Codex OAuth.";
        else
            TempData["Error"] = generation.Error;

        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserFlows(
        int id,
        List<string> userTypes,
        List<string> purposes,
        List<string> steps,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var design = await _db.ProjectKickStartDesigns
            .FirstOrDefaultAsync(item => item.Id == id && item.CreatedById == userId, cancellationToken);
        if (design is null)
            return NotFound();

        var blueprint = DeserializeBlueprint(design.BlueprintJson);
        if (blueprint is null)
            return NotFound();

        if (userTypes.Count == 0 || userTypes.Count > 8 ||
            purposes.Count != userTypes.Count || steps.Count != userTypes.Count)
        {
            TempData["Error"] = "User flows could not be saved because the submitted rows were invalid.";
            return RedirectToAction(nameof(Index), new { id });
        }

        var updatedFlows = new List<ProjectKickStartUserFlow>(userTypes.Count);
        for (var index = 0; index < userTypes.Count; index++)
        {
            var userType = userTypes[index].Trim();
            var purpose = purposes[index].Trim();
            var flowSteps = steps[index]
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(8)
                .ToList();
            if (string.IsNullOrWhiteSpace(userType) || userType.Length > 100 ||
                string.IsNullOrWhiteSpace(purpose) || purpose.Length > 300 ||
                flowSteps.Count == 0 || flowSteps.Any(step => step.Length > 100))
            {
                TempData["Error"] = "Each user flow needs a valid user type, purpose, and at least one step. Keep every step under 100 characters.";
                return RedirectToAction(nameof(Index), new { id });
            }

            updatedFlows.Add(new ProjectKickStartUserFlow(userType, purpose, flowSteps));
        }

        design.BlueprintJson = JsonSerializer.Serialize(blueprint with { UserFlows = updatedFlows }, JsonOptions);
        await _db.SaveChangesAsync(cancellationToken);
        TempData["Success"] = "User flows updated.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateArchitectureDiagram(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var design = await _db.ProjectKickStartDesigns
            .FirstOrDefaultAsync(item => item.Id == id && item.CreatedById == userId, cancellationToken);
        if (design is null)
            return NotFound();

        var blueprint = DeserializeBlueprint(design.BlueprintJson);
        if (blueprint is null)
            return NotFound();

        var authStatus = await _codexAuthService.GetStatusAsync(cancellationToken);
        if (!authStatus.IsAuthenticated || !authStatus.IsChatGptLogin)
        {
            TempData["Error"] = "Connect Softracker using Sign in with ChatGPT before generating an architecture diagram.";
            return RedirectToAction(nameof(Index), new { id });
        }

        var generation = await GenerateAndAttachArchitectureAsync(design, blueprint, cancellationToken);
        if (!generation.Succeeded)
        {
            TempData["Error"] = generation.Error;
            return RedirectToAction(nameof(Index), new { id });
        }
        TempData["Success"] = "Architecture diagram generated from the complete blueprint.";
        return RedirectToAction(nameof(Index), new { id });
    }

    private async Task<CodexProjectImageGenerationResult> GenerateAndAttachArchitectureAsync(
        ProjectKickStartDesign design,
        ProjectKickStartBlueprint blueprint,
        CancellationToken cancellationToken)
    {
        var generation = await _codexService.GenerateProjectKickStartImagesAsync(
            blueprint,
            design.Id,
            1,
            architectureDiagram: true,
            cancellationToken: cancellationToken);
        var generatedImage = generation.Images.FirstOrDefault();
        if (generatedImage is null)
            return generation;

        design.BlueprintJson = JsonSerializer.Serialize(
            blueprint with { ArchitectureImageFileName = generatedImage.FileName },
            JsonOptions);
        await _db.SaveChangesAsync(cancellationToken);
        return generation;
    }

    private async Task<CodexProjectImageGenerationResult> GenerateAndAttachImagesAsync(
        ProjectKickStartDesign design,
        ProjectKickStartBlueprint blueprint,
        int maxImages,
        CancellationToken cancellationToken)
    {
        var generation = await _codexService.GenerateProjectKickStartImagesAsync(
            blueprint,
            design.Id,
            maxImages,
            cancellationToken: cancellationToken);

        if (generation.Images.Count == 0 || blueprint.VisualPlan is null)
            return generation;

        var generatedByIndex = generation.Images.ToDictionary(image => image.PageIndex);
        var updatedSamples = blueprint.VisualPlan.PageSamples
            .Select((sample, index) => generatedByIndex.TryGetValue(index, out var image)
                ? sample with { ImageFileName = image.FileName }
                : sample)
            .ToList();
        var updatedBlueprint = blueprint with
        {
            VisualPlan = blueprint.VisualPlan with { PageSamples = updatedSamples }
        };
        design.BlueprintJson = JsonSerializer.Serialize(updatedBlueprint, JsonOptions);
        await _db.SaveChangesAsync(cancellationToken);
        return generation;
    }

    [HttpGet]
    public async Task<IActionResult> Image(int id, string file, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var design = await _db.ProjectKickStartDesigns
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.CreatedById == userId, cancellationToken);
        if (design is null)
            return NotFound();

        var blueprint = DeserializeBlueprint(design.BlueprintJson);
        var isReferenced = string.Equals(blueprint?.ArchitectureImageFileName, file, StringComparison.Ordinal) ||
            blueprint?.VisualPlan?.PageSamples.Any(sample =>
                string.Equals(sample.ImageFileName, file, StringComparison.Ordinal)) == true;
        if (!isReferenced)
            return NotFound();

        var path = _codexService.ResolveProjectKickStartImagePath(id, file);
        if (path is null)
            return NotFound();

        var contentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };
        return PhysicalFile(path, contentType, enableRangeProcessing: true);
    }

    [HttpGet]
    public async Task<IActionResult> ExportMarkdown(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var design = await _db.ProjectKickStartDesigns
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.CreatedById == userId, cancellationToken);
        if (design is null)
            return NotFound();

        var blueprint = DeserializeBlueprint(design.BlueprintJson);
        if (blueprint is null)
            return NotFound();

        var markdown = BuildMarkdown(design, blueprint);
        var safeTitle = string.Concat(blueprint.Title.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '-' : character)).Trim();
        var fileName = $"{(string.IsNullOrWhiteSpace(safeTitle) ? "project-kickstart" : safeTitle)}.md";
        return File(Encoding.UTF8.GetBytes(markdown), "text/markdown; charset=utf-8", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var design = await _db.ProjectKickStartDesigns
            .FirstOrDefaultAsync(item => item.Id == id && item.CreatedById == userId, cancellationToken);
        if (design is not null)
        {
            _db.ProjectKickStartDesigns.Remove(design);
            await _db.SaveChangesAsync(cancellationToken);
            await _codexService.DeleteProjectKickStartImagesAsync(design.Id);
            TempData["Success"] = "Saved blueprint deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<List<ProjectKickStartDesign>> LoadHistory(string userId, CancellationToken cancellationToken) =>
        await _db.ProjectKickStartDesigns
            .AsNoTracking()
            .Where(item => item.CreatedById == userId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(30)
            .ToListAsync(cancellationToken);

    private static ProjectKickStartPageViewModel BuildPage(ProjectKickStartDesign? selected, List<ProjectKickStartDesign> history)
    {
        var blueprint = selected is null ? null : DeserializeBlueprint(selected.BlueprintJson);

        return new ProjectKickStartPageViewModel
        {
            Input = selected is null
                ? new ProjectKickStartInputViewModel()
                : new ProjectKickStartInputViewModel
                {
                    Summary = selected.Summary,
                    Technology = selected.Technology,
                    CloudHostingTarget = selected.CloudHostingTarget,
                    UserCount = selected.UserCount,
                    Features = selected.Features,
                    UiDirection = selected.UiDirection
                },
            SelectedDesign = selected,
            Blueprint = blueprint,
            History = history
        };
    }

    private async Task PopulateCodexStatusAsync(ProjectKickStartPageViewModel page, CancellationToken cancellationToken)
    {
        var status = await _codexAuthService.GetStatusAsync(cancellationToken);
        page.IsCodexAvailable = status.IsAvailable;
        page.IsCodexConnected = status.IsAuthenticated;
        page.IsCodexChatGptLogin = status.IsChatGptLogin;
        page.CodexStatusMessage = status.Message;

    }

    private static ProjectKickStartBlueprint? DeserializeBlueprint(string blueprintJson)
    {
        try
        {
            return JsonSerializer.Deserialize<ProjectKickStartBlueprint>(blueprintJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildMarkdown(ProjectKickStartDesign design, ProjectKickStartBlueprint blueprint)
    {
        var markdown = new StringBuilder()
            .AppendLine($"# {blueprint.Title}").AppendLine()
            .AppendLine($"_Generated {design.CreatedAtUtc:yyyy-MM-dd HH:mm} UTC using {blueprint.SourceMode}._").AppendLine()
            .AppendLine("## Project input").AppendLine()
            .AppendLine($"- Summary: {design.Summary}")
            .AppendLine($"- Technology: {design.Technology}")
            .AppendLine($"- Hosting: {design.CloudHostingTarget ?? "Not specified"}")
            .AppendLine($"- Expected users: {design.UserCount}")
            .AppendLine($"- Features: {design.Features}").AppendLine()
            .AppendLine("## Overview").AppendLine()
            .AppendLine(blueprint.RecommendedArchitecture).AppendLine()
            .AppendLine("### Main components");
        AppendList(markdown, blueprint.MainComponents);
        markdown.AppendLine("### Scaling advice").AppendLine().AppendLine(blueprint.ScalingAdvice).AppendLine()
            .AppendLine("### Security baseline").AppendLine().AppendLine(blueprint.SecurityNotes).AppendLine()
            .AppendLine("## MVP").AppendLine();
        if (blueprint.Mvp is not null)
        {
            markdown.AppendLine(blueprint.Mvp.SystemOverview).AppendLine()
                .AppendLine($"**Problem solved:** {blueprint.Mvp.ProblemSolved}").AppendLine()
                .AppendLine($"**Core value:** {blueprint.Mvp.CoreValue}").AppendLine()
                .AppendLine("### Core capabilities");
            foreach (var capability in blueprint.Mvp.CoreCapabilities)
                markdown.AppendLine($"- **{capability.Name}:** {capability.Description} Acceptance: {capability.AcceptanceOutcome}");
            markdown.AppendLine().AppendLine("### Out of scope");
            AppendList(markdown, blueprint.Mvp.OutOfScope);
            markdown.AppendLine("### Success criteria");
            AppendList(markdown, blueprint.Mvp.SuccessCriteria);
        }

        markdown.AppendLine("## User flows").AppendLine();
        foreach (var flow in blueprint.UserFlows ?? [])
            markdown.AppendLine($"- **{flow.UserType} — {flow.Purpose}:** {string.Join(" → ", flow.Steps)}");

        markdown.AppendLine().AppendLine("## Architecture and deployment").AppendLine();
        foreach (var service in blueprint.DeploymentServices)
            markdown.AppendLine($"- **{service.Module}:** {service.RecommendedService} ({service.Runtime}) — {service.Reason}");
        markdown.AppendLine().AppendLine($"**Database:** {blueprint.DatabaseStorageRecommendation}").AppendLine()
            .AppendLine($"**API/backend:** {blueprint.ApiBackendRecommendation}").AppendLine()
            .AppendLine("## Database schema").AppendLine();
        foreach (var table in blueprint.TableSchemas)
        {
            markdown.AppendLine($"### {table.Name}").AppendLine().AppendLine(table.Purpose).AppendLine()
                .AppendLine("| Column | Type | Key | Notes |").AppendLine("|---|---|---|---|");
            foreach (var column in table.Columns)
            {
                var key = column.IsPrimaryKey ? "PK" : column.IsForeignKey ? "FK" : string.Empty;
                markdown.AppendLine($"| {column.Name} | {column.Type} | {key} | {column.Notes} |");
            }
            if (table.Relationships.Count > 0)
                markdown.AppendLine().AppendLine($"Relationships: {string.Join("; ", table.Relationships)}");
            markdown.AppendLine();
        }

        markdown.AppendLine("## Estimated monthly cost").AppendLine()
            .AppendLine($"**{blueprint.CostEstimate.MonthlyRange} {blueprint.CostEstimate.Currency}** — {blueprint.CostEstimate.Summary}").AppendLine();
        foreach (var item in blueprint.CostEstimate.LineItems)
            markdown.AppendLine($"- **{item.Name}:** {item.MonthlyRange} — {item.Notes}");
        markdown.AppendLine().AppendLine("### Assumptions");
        AppendList(markdown, blueprint.CostEstimate.Assumptions);
        markdown.AppendLine("### Cost optimizations");
        AppendList(markdown, blueprint.CostEstimate.CostOptimizations);

        markdown.AppendLine("## Image samples").AppendLine();
        markdown.AppendLine($"- **Architecture diagram:** `{blueprint.ArchitectureImageFileName ?? "not generated"}`");
        foreach (var sample in blueprint.VisualPlan?.PageSamples ?? [])
            markdown.AppendLine($"- **{sample.PageName}** (`{sample.Route}`): {sample.Purpose} — image file: `{sample.ImageFileName ?? "not generated"}`");

        markdown.AppendLine().AppendLine("## Risks and trade-offs");
        AppendList(markdown, blueprint.RisksTradeoffs);
        markdown.AppendLine("## Next steps");
        AppendList(markdown, blueprint.NextSteps);
        if (!string.IsNullOrWhiteSpace(blueprint.Notice))
            markdown.AppendLine($"_{blueprint.Notice}_");
        return markdown.ToString();
    }

    private static void AppendList(StringBuilder markdown, IEnumerable<string> items)
    {
        foreach (var item in items)
            markdown.AppendLine($"- {item}");
        markdown.AppendLine();
    }
}
