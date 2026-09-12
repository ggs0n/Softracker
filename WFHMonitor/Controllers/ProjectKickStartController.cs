using System.Security.Claims;
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

        TempData["Success"] = "Project blueprint generated and saved.";
        return RedirectToAction(nameof(Index), new { id = design.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateImages(int id, CancellationToken cancellationToken)
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

        var generation = await _codexService.GenerateProjectKickStartImagesAsync(
            blueprint,
            design.Id,
            cancellationToken: cancellationToken);

        if (generation.Images.Count > 0)
        {
            var generatedByIndex = generation.Images.ToDictionary(image => image.PageIndex);
            var updatedSamples = blueprint.VisualPlan.PageSamples
                .Select((sample, index) => generatedByIndex.TryGetValue(index, out var image)
                    ? sample with { ImageFileName = image.FileName }
                    : sample)
                .ToList();
            blueprint = blueprint with
            {
                VisualPlan = blueprint.VisualPlan with { PageSamples = updatedSamples }
            };
            design.BlueprintJson = JsonSerializer.Serialize(blueprint, JsonOptions);
            await _db.SaveChangesAsync(cancellationToken);
        }

        if (generation.Succeeded)
            TempData["Success"] = $"Generated {generation.Images.Count} real page images using Codex OAuth.";
        else
            TempData["Error"] = generation.Error;

        return RedirectToAction(nameof(Index), new { id });
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
        var isReferenced = blueprint?.VisualPlan?.PageSamples.Any(sample =>
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
}
