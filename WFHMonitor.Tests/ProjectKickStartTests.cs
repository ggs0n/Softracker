using System.Reflection;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using WFHMonitor.Services;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Tests;

public class ProjectKickStartTests
{
    [Fact]
    public void TargetPlatform_RequiresWebMobileOrBoth()
    {
        var input = new ProjectKickStartInputViewModel
        {
            Summary = "Repair booking",
            Technology = "ASP.NET Core",
            UserCount = "100 users",
            Features = "Booking",
            TargetWeb = false,
            TargetMobile = false
        };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(input, new ValidationContext(input), results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result => result.ErrorMessage == "Choose Web, Mobile, or both.");
        input.TargetMobile = true;
        Assert.Equal("Mobile", input.TargetPlatforms);
    }

    [Fact]
    public void Generate_UsesCodexOAuthAndStructuredBlueprintSchema()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var controller = File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Controllers", "ProjectKickStartController.cs"));
        var codexService = File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Services", "CodexAgentService.cs"));

        Assert.Contains("_codexAuthService.GetStatusAsync", controller);
        Assert.Contains("GenerateProjectKickStartAsync", controller);
        Assert.Contains("GenerateAndAttachImagesAsync(design, blueprint", controller);
        Assert.Contains("Project blueprint generated with", controller);
        Assert.Contains("BuildProjectKickStartPrompt", codexService);
        Assert.Contains("BuildProjectKickStartSchema", codexService);
        Assert.Contains("--output-schema", codexService);
        Assert.Contains("StandardOutputEncoding = Encoding.UTF8", codexService);
        Assert.Contains("Theme or UI description: {uiDirection}", codexService);
        Assert.Contains("Target platforms: {input.TargetPlatforms}", codexService);
        Assert.Contains("Not specified. Choose the most suitable UI theme", codexService);
        Assert.DoesNotContain("SKILLS/IMAGEDESIGN", codexService, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProjectKickStart_IsAvailableFromLeftNavigation()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var layout = File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Views", "Shared", "_Layout.cshtml"));
        var view = File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Views", "ProjectKickStart", "Index.cshtml"));

        Assert.Contains("asp-controller=\"ProjectKickStart\"", layout);
        Assert.Contains("> Project KickStart", layout);
        Assert.Contains("sidebar-codex-usage", layout);
        Assert.Contains("https://chatgpt.com/codex/settings/usage", layout);
        Assert.Contains("Check remaining quota", layout);
        Assert.Contains("Generate with Codex AI", view);
        Assert.Contains("Connect Codex to Generate", view);
        Assert.Contains("Codex is designing the architecture and generating up to two page images", view);
        Assert.Contains("Database Schema", view);
        Assert.Contains("Risks & Plan", view);
        Assert.Contains("data-pk-tab=\"mvp\"", view);
        Assert.Contains("What the MVP system does", view);
        Assert.Contains("data-pk-tab=\"user-flow\"", view);
        Assert.Contains("Identified user types", view);
        Assert.Contains("asp-action=\"UpdateUserFlows\"", view);
        Assert.Contains("Edit user flows manually", view);
        Assert.Contains("Save user flows", view);
        Assert.Contains("blueprint with { UserFlows = updatedFlows }", File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Controllers", "ProjectKickStartController.cs")));
        Assert.Contains("data-pk-tab=\"page-images\"", view);
        Assert.Contains("AI-generated page images", view);
        Assert.Contains("data-copy-prompt", view);
        Assert.Contains("asp-for=\"Input.UiDirection\"", view);
        Assert.Contains("Theme / Describe UI", view);
        Assert.Contains("asp-for=\"Input.TargetWeb\"", view);
        Assert.Contains("asp-for=\"Input.TargetMobile\"", view);
        Assert.Contains("pk-flow-step-arrow", view);
        Assert.Contains("flex-wrap:wrap", view);
        Assert.Contains("groupedUserFlows", view);
        Assert.Contains("FixGeneratedText", view);
        Assert.Contains("asp-action=\"ExportMarkdown\"", view);
        Assert.Contains("Download .md", view);
        Assert.DoesNotContain("data-pk-tab=\"edit-all\"", view);
        Assert.Contains("asp-action=\"UpdateBlueprint\"", view);
        Assert.Contains("data-edit-section=\"overview\"", view);
        Assert.Contains("data-edit-section=\"mvp\"", view);
        Assert.Contains("data-edit-section=\"page-images\"", view);
        Assert.Contains("data-edit-section=\"architecture\"", view);
        Assert.Contains("data-edit-section=\"schema\"", view);
        Assert.Contains("data-edit-section=\"risks\"", view);
        Assert.Contains("Save Overview", view);
        Assert.Contains("Save Architecture", view);
        Assert.Contains("ArchitectureImageFileName = existingBlueprint.ArchitectureImageFileName", File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Controllers", "ProjectKickStartController.cs")));
        Assert.Contains("BuildMarkdown", File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Controllers", "ProjectKickStartController.cs")));
    }

    [Fact]
    public void ImageGeneration_UsesBuiltInCodexOAuthWithoutApiKeyFallback()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var controller = File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Controllers", "ProjectKickStartController.cs"));
        var codexService = File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Services", "CodexAgentService.cs"));
        var authService = File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Services", "CodexAuthService.cs"));
        var view = File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Views", "ProjectKickStart", "Index.cshtml"));

        Assert.Contains("$imagegen", codexService);
        Assert.Contains("IMAGEGEN_UNAVAILABLE", codexService);
        Assert.Contains("Math.Clamp(maxImages, 1, 5)", codexService);
        Assert.Contains("Take(requestedImageCount)", codexService);
        Assert.Contains("medium-quality preview images", codexService);
        Assert.Contains("1024 x 576 pixels", codexService);
        Assert.Contains("\"workspace-write\"", codexService);
        Assert.Contains("stripSensitiveEnvironment: true", codexService);
        Assert.Contains("promptViaStandardInput: true", codexService);
        Assert.Contains("StandardInput.WriteAsync(imagePrompt.AsMemory()", codexService);
        Assert.Contains("args.Add(promptViaStandardInput ? \"-\" : prompt)", codexService);
        Assert.Contains("GetCodexGeneratedImagesRoot", codexService);
        Assert.Contains("SnapshotGeneratedImagePaths", codexService);
        Assert.Contains("WaitForGeneratedImagesAsync", codexService);
        Assert.Contains("generatedImagePaths.Count >= pageSamples.Count", codexService);
        Assert.Contains("TryKill(process)", codexService);
        Assert.Contains("IMAGEGEN_COMPLETE", codexService);
        Assert.Contains("Do not run shell commands", codexService);
        Assert.Contains("Never call an image API", codexService);
        Assert.DoesNotContain("api.openai.com", codexService, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/v1/images", codexService, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("authenticationDetail.Contains(\"ChatGPT\"", authService);
        Assert.Contains("!authStatus.IsChatGptLogin", controller);
        Assert.Contains("GenerateProjectKickStartImagesAsync", controller);
        Assert.Contains("ResolveProjectKickStartImagePath", controller);
        Assert.Contains("Generate with Codex OAuth", view);
        Assert.Contains("asp-action=\"GenerateImages\"", view);
        Assert.Contains("Generate 5 Images", view);
        Assert.Contains("name=\"count\" value=\"5\"", view);
        Assert.Contains("asp-action=\"GenerateArchitectureDiagram\"", view);
        Assert.Contains("Generate Architecture Diagram", view);
        Assert.Contains("BuildArchitectureDiagramSample", codexService);
        Assert.Contains("Ignore the blueprint's UI theme, palette, typography", codexService);
        Assert.Contains("professional cloud solution-architecture style", codexService);
        Assert.Contains("architectureDiagram: true", controller);
        Assert.Contains("GenerateAndAttachArchitectureAsync(design, currentBlueprint", controller);
        Assert.Contains("architectureDiagram ? \"architecture\" : \"page\"", codexService);
        Assert.Contains("pk-generated-page-image", view);
        Assert.Contains("No API key is used", view);
    }

    [Fact]
    public void CodexSchema_RequiresMvpUserFlowsAndTwoPageImageSamples()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var schemaMethod = typeof(CodexBugScanService).GetMethod(
            "BuildProjectKickStartSchema",
            BindingFlags.NonPublic | BindingFlags.Static);
        var schema = Assert.IsType<string>(schemaMethod?.Invoke(null, null));
        using var document = JsonDocument.Parse(schema);
        var root = document.RootElement;
        var required = root.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToHashSet();

        Assert.Contains("mvp", required);
        Assert.Contains("userFlows", required);
        Assert.Contains("visualPlan", required);
        Assert.Equal(4, root.GetProperty("properties").GetProperty("mvp").GetProperty("properties").GetProperty("coreCapabilities").GetProperty("minItems").GetInt32());
        Assert.Equal(3, root.GetProperty("properties").GetProperty("userFlows").GetProperty("items").GetProperty("properties").GetProperty("steps").GetProperty("minItems").GetInt32());
        Assert.Equal(5, root.GetProperty("properties").GetProperty("visualPlan").GetProperty("properties").GetProperty("pageSamples").GetProperty("minItems").GetInt32());
        Assert.Equal(5, root.GetProperty("properties").GetProperty("visualPlan").GetProperty("properties").GetProperty("pageSamples").GetProperty("maxItems").GetInt32());
        Assert.Contains("one separate horizontal 16:9 medium-quality preview concept per page", File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Services", "CodexAgentService.cs")));
    }

    [Fact]
    public void OlderSavedBlueprint_RemainsReadableWithoutNewSections()
    {
        const string json = """
            {"title":"Old","recommendedArchitecture":"Architecture","mainComponents":[],"deploymentServices":[],"databaseStorageRecommendation":"Database","apiBackendRecommendation":"API","scalingAdvice":"Scale","securityNotes":"Security","costEstimate":{"currency":"USD","monthlyRange":"$1","summary":"Cost","lineItems":[],"assumptions":[],"costOptimizations":[]},"risksTradeoffs":[],"nextSteps":[],"tableSchemas":[],"sourceMode":"Rules-based","notice":null}
            """;

        var blueprint = JsonSerializer.Deserialize<ProjectKickStartBlueprint>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(blueprint);
        Assert.Null(blueprint.Mvp);
        Assert.Null(blueprint.UserFlows);
        Assert.Null(blueprint.VisualPlan);
    }

}
