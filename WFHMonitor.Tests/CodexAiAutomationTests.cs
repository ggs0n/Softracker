using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WFHMonitor.AutomationService.Configuration;
using WFHMonitor.AutomationService.Services;
using WFHMonitor.Models;

namespace WFHMonitor.Tests;

public sealed class CodexAiAutomationTests
{
    [Fact]
    public async Task Scan_RejectsWorkspaceOutsideConfiguredRoot()
    {
        using var fixture = new AutomationFixture();
        var service = fixture.CreateService(
            """{"findings":[]}""");
        var outside = Path.Combine(
            Path.GetTempPath(),
            $"outside-{Guid.NewGuid():N}");

        var result = await service.ScanProjectAsync(
            Project(),
            outside);

        Assert.False(result.Succeeded);
        fixture.Codex.Verify(
            client => client.RunTurnAsync(
                It.IsAny<CodexTurnRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Scan_HandlesMalformedStructuredResponse()
    {
        using var fixture = new AutomationFixture();
        var service = fixture.CreateService("not-json");
        var workspace = fixture.CreateWorkspace();

        var result = await service.ScanProjectAsync(
            Project(),
            workspace);

        Assert.False(result.Succeeded);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task Scan_PassesStrictSchemaAndReadOnlyWorkspaceToCodex()
    {
        using var fixture = new AutomationFixture();
        var service = fixture.CreateService(
            """
            {
              "findings": [
                {
                  "title": "Missing authorization",
                  "description": "An endpoint lacks an ownership check.",
                  "workflow": "Open another user's record.",
                  "stepsToReproduce": "Call the endpoint with another id.",
                  "moduleImpacted": "Projects",
                  "severity": "High",
                  "screenshotPaths": []
                }
              ]
            }
            """);
        var workspace = fixture.CreateWorkspace();

        var result = await service.ScanProjectAsync(
            Project(),
            workspace);

        Assert.True(result.Succeeded);
        Assert.Single(result.Findings);
        fixture.Codex.Verify(
            client => client.RunTurnAsync(
                It.Is<CodexTurnRequest>(request =>
                    request.WorkingDirectory == workspace
                    && !request.AllowWrites
                    && request.OutputSchema != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ModuleImages_UseProviderAuthAndAreCappedAtTen()
    {
        using var fixture = new AutomationFixture();
        var image = new CodexGeneratedImage(
            "completed",
            Convert.ToBase64String(
                new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "module image prompt",
            null);
        var service = fixture.CreateService(
            "Generated module images.",
            Enumerable.Repeat(image, 12).ToArray());
        fixture.Codex
            .Setup(client => client.SupportsImageGenerationAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var modules = Enumerable.Range(1, 12)
            .Select(index => new WFHMonitor.Services.Interfaces
                .AiModuleImageContext(
                    $"Module {index}",
                    $"Responsibility {index}",
                    ".NET 8",
                    "Container",
                    [],
                    [],
                    []))
            .ToArray();

        var result = await service.GenerateModuleImagesAsync(
            new(
                "System",
                "Summary",
                "React and .NET 8",
                modules));

        Assert.True(result.Succeeded);
        Assert.Equal(10, result.Images.Count);
        Assert.Equal("Module 1", result.Images[0].ModuleName);
        Assert.Equal("Module 10", result.Images[^1].ModuleName);
        Assert.All(
            result.Images,
            generated => Assert.Equal("image/png", generated.MimeType));
        fixture.Codex.Verify(
            client => client.RunTurnAsync(
                It.Is<CodexTurnRequest>(request =>
                    !request.AllowWrites
                    && request.OutputSchema == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ModuleImages_RejectMalformedProviderImageData()
    {
        using var fixture = new AutomationFixture();
        var service = fixture.CreateService(
            "Generated module image.",
            [
                new CodexGeneratedImage(
                    "completed",
                    "not-base64",
                    null,
                    null)
            ]);
        fixture.Codex
            .Setup(client => client.SupportsImageGenerationAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await service.GenerateModuleImagesAsync(
            new(
                "System",
                "Summary",
                "React and .NET 8",
                [
                    new(
                        "Registration",
                        "Registers users",
                        ".NET 8",
                        "Container",
                        [],
                        [],
                        [])
                ]));

        Assert.False(result.Succeeded);
        Assert.Empty(result.Images);
    }

    private static ChangeRequest Project() => new()
    {
        Id = 4,
        CrNumber = "CR-0004",
        Title = "Test project",
        CreatedById = "admin"
    };

    private sealed class AutomationFixture : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"wfhmonitor-automation-tests-{Guid.NewGuid():N}");
        private readonly string _automationProject =
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "../../../../WFHMonitor.AutomationService"));

        public Mock<ICodexAppServerClient> Codex { get; } = new();

        public CodexAiAutomationService CreateService(
            string output,
            IReadOnlyList<CodexGeneratedImage>? generatedImages = null)
        {
            Directory.CreateDirectory(_root);
            Codex
                .Setup(client => client.RunTurnAsync(
                    It.IsAny<CodexTurnRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CodexTurnResult(
                    output,
                    [],
                    [],
                    generatedImages));
            var settings = Options.Create(new AiAutomationSettings
            {
                WorkspaceRoot = _root,
                PromptDirectory = Path.Combine(
                    _automationProject,
                    "Prompts"),
                SchemaDirectory = Path.Combine(
                    _automationProject,
                    "Schemas")
            });
            var environment = new Mock<IWebHostEnvironment>();
            environment.SetupGet(value => value.ContentRootPath)
                .Returns(_automationProject);
            var assets = new PromptAssetLoader(
                environment.Object,
                settings);
            return new CodexAiAutomationService(
                Codex.Object,
                assets,
                settings,
                environment.Object,
                NullLogger<CodexAiAutomationService>.Instance);
        }

        public string CreateWorkspace()
        {
            var path = Path.Combine(
                _root,
                $"workspace-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
