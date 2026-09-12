using System.Reflection;
using System.Text.Json;
using WFHMonitor.Services;
using WFHMonitor.ViewModels;

namespace WFHMonitor.Tests;

public class ProjectKickStartTests
{
    [Fact]
    public void Generate_UsesCodexOAuthAndStructuredBlueprintSchema()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var controller = File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Controllers", "ProjectKickStartController.cs"));
        var codexService = File.ReadAllText(Path.Combine(repositoryRoot, "WFHMonitor", "Services", "CodexAgentService.cs"));

        Assert.Contains("_codexAuthService.GetStatusAsync", controller);
        Assert.Contains("GenerateProjectKickStartAsync", controller);
        Assert.Contains("GenerateAndAttachImagesAsync(design, blueprint", controller);
        Assert.Contains("Project blueprint generated and saved with", controller);
        Assert.Contains("BuildProjectKickStartPrompt", codexService);
        Assert.Contains("BuildProjectKickStartSchema", codexService);
        Assert.Contains("--output-schema", codexService);
        Assert.Contains("StandardOutputEncoding = Encoding.UTF8", codexService);
        Assert.Contains("Theme or UI description: {uiDirection}", codexService);
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
        Assert.Contains("Generate with Codex AI", view);
        Assert.Contains("Connect Codex to Generate", view);
        Assert.Contains("Codex is designing the architecture and generating up to two page images", view);
        Assert.Contains("Database Schema", view);
        Assert.Contains("Risks & Plan", view);
        Assert.Contains("data-pk-tab=\"mvp\"", view);
        Assert.Contains("What the MVP system does", view);
        Assert.Contains("data-pk-tab=\"user-flow\"", view);
        Assert.Contains("Identified user types", view);
        Assert.Contains("data-pk-tab=\"page-images\"", view);
        Assert.Contains("AI-generated page images", view);
        Assert.Contains("data-copy-prompt", view);
        Assert.Contains("asp-for=\"Input.UiDirection\"", view);
        Assert.Contains("Theme / Describe UI", view);
        Assert.Contains("pk-flow-step-arrow", view);
        Assert.Contains("flex-wrap:wrap", view);
        Assert.Contains("groupedUserFlows", view);
        Assert.Contains("FixGeneratedText", view);
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
