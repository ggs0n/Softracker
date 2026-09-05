using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using WFHMonitor.AutomationService.Configuration;

namespace WFHMonitor.AutomationService.Services;

public sealed class PromptAssetLoader
{
    private readonly string _promptDirectory;
    private readonly string _schemaDirectory;

    public PromptAssetLoader(
        IWebHostEnvironment environment,
        IOptions<AiAutomationSettings> settings)
    {
        _promptDirectory = ResolveDirectory(
            environment.ContentRootPath,
            settings.Value.PromptDirectory);
        _schemaDirectory = ResolveDirectory(
            environment.ContentRootPath,
            settings.Value.SchemaDirectory);
    }

    public async Task<string> ReadPromptAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var path = SafeAssetPath(_promptDirectory, name, ".txt");
        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public async Task<JsonNode> ReadSchemaAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var path = SafeAssetPath(_schemaDirectory, name, ".json");
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonNode.Parse(json)
               ?? throw new InvalidOperationException(
                   $"AI schema '{name}' is invalid.");
    }

    private static string ResolveDirectory(
        string contentRoot,
        string configuredDirectory)
    {
        if (string.IsNullOrWhiteSpace(configuredDirectory))
            throw new InvalidOperationException(
                "An AI asset directory is required.");
        return Path.GetFullPath(
            Path.IsPathRooted(configuredDirectory)
                ? configuredDirectory
                : Path.Combine(contentRoot, configuredDirectory));
    }

    private static string SafeAssetPath(
        string root,
        string name,
        string extension)
    {
        if (string.IsNullOrWhiteSpace(name)
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("The AI asset name is invalid.");
        }

        var path = Path.GetFullPath(
            Path.Combine(root, $"{name}{extension}"));
        var relative = Path.GetRelativePath(root, path);
        if (relative.StartsWith("..", StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException(
                "The AI asset path is outside its configured directory.");
        }

        return path;
    }
}
