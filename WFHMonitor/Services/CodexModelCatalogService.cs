using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public sealed class CodexModelCatalogService : ICodexModelCatalogService
{
    private const string CacheKey = "codex:model-catalog";
    private readonly CodexSettings _settings;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CodexModelCatalogService> _logger;

    public CodexModelCatalogService(
        IOptions<CodexSettings> settings,
        IMemoryCache cache,
        ILogger<CodexModelCatalogService> logger)
    {
        _settings = settings.Value ?? new CodexSettings();
        _cache = cache;
        _logger = logger;
    }

    public async Task<CodexModelCatalogResult> GetModelsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (forceRefresh)
            _cache.Remove(CacheKey);

        if (_cache.TryGetValue<CodexModelCatalogResult>(CacheKey, out var cached) && cached is not null)
            return cached;

        CodexModelCatalogResult result;
        try
        {
            result = await LoadFromAppServerAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not refresh the Codex model catalog.");
            result = BuildFallback("Live model refresh is unavailable. Showing built-in choices; check Codex sign-in or try Refresh again.");
        }

        _cache.Set(CacheKey, result, result.IsLive ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(2));
        return result;
    }

    private async Task<CodexModelCatalogResult> LoadFromAppServerAsync(CancellationToken cancellationToken)
    {
        var cliPath = ResolveCliPath();
        using var process = new Process { StartInfo = BuildStartInfo(cliPath) };
        if (!process.Start())
            throw new InvalidOperationException("Unable to start Codex app server.");

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));

            await WriteMessageAsync(process, new
            {
                method = "initialize",
                id = 1,
                @params = new { clientInfo = new { name = "softracker", title = "Softracker", version = "1.0.0" } }
            }, timeout.Token);
            await ReadResponseAsync(process, 1, timeout.Token);

            await WriteMessageAsync(process, new { method = "initialized", @params = new { } }, timeout.Token);

            var models = new List<CodexModelCatalogItem>();
            string? cursor = null;
            var requestId = 2;
            do
            {
                await WriteMessageAsync(process, new
                {
                    method = "model/list",
                    id = requestId,
                    @params = new { cursor, limit = 100, includeHidden = false }
                }, timeout.Token);

                using var response = await ReadResponseAsync(process, requestId, timeout.Token);
                if (!response.RootElement.TryGetProperty("result", out var result))
                    throw new InvalidOperationException("Codex model/list did not return a result.");

                if (result.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        var id = GetString(item, "id") ?? GetString(item, "model");
                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        var efforts = ReadEfforts(item);
                        var defaultEffort = GetString(item, "defaultReasoningEffort") ?? CodexAiDefaults.ReasoningEffort;
                        if (efforts.Count == 0)
                            efforts.AddRange(CodexAiDefaults.ReasoningEfforts);

                        models.Add(new CodexModelCatalogItem(
                            id,
                            GetString(item, "displayName") ?? id,
                            defaultEffort,
                            efforts,
                            GetBoolean(item, "isDefault"),
                            ReadUpgrade(item)));
                    }
                }

                cursor = GetString(result, "nextCursor");
                requestId++;
            }
            while (!string.IsNullOrWhiteSpace(cursor) && requestId < 12);

            var unique = models
                .GroupBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderByDescending(model => model.IsDefault)
                .ThenBy(model => model.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (unique.Count == 0)
                throw new InvalidOperationException("Codex returned an empty model catalog.");

            return new CodexModelCatalogResult(unique, true, $"{unique.Count} available models loaded from your installed Codex CLI.");
        }
        finally
        {
            TryKill(process);
            try { await stderrTask; } catch { }
        }
    }

    private string ResolveCliPath()
    {
        var configured = string.IsNullOrWhiteSpace(_settings.CliPath) ? "codex" : _settings.CliPath.Trim();
        var candidates = new List<string>();
        var envPath = Environment.GetEnvironmentVariable("CODEX_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(envPath)) candidates.Add(envPath);
        candidates.Add(configured);

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                candidates.Add(Path.Combine(appData, "npm", "codex.cmd"));
                candidates.Add(Path.Combine(appData, "npm", "codex.ps1"));
                candidates.Add(Path.Combine(appData, "npm", "codex.exe"));
            }
        }

        return candidates
            .Select(value => Environment.ExpandEnvironmentVariables(value.Trim()))
            .FirstOrDefault(File.Exists) ?? configured;
    }

    private static ProcessStartInfo BuildStartInfo(string cliPath)
    {
        var isPowerShell = cliPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
        var isCommand = cliPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                        cliPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo
        {
            FileName = isPowerShell ? "powershell" : isCommand ? "cmd.exe" : cliPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (isPowerShell)
        {
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(cliPath);
        }
        else if (isCommand)
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/s");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(cliPath);
        }

        startInfo.ArgumentList.Add("app-server");
        return startInfo;
    }

    private static async Task WriteMessageAsync(Process process, object message, CancellationToken cancellationToken)
    {
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(message).AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    private static async Task<JsonDocument> ReadResponseAsync(Process process, int id, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            if (line is null)
                throw new InvalidOperationException("Codex app server closed before replying.");

            JsonDocument? document = null;
            try
            {
                document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("id", out var responseId) && responseId.GetInt32() == id)
                {
                    if (document.RootElement.TryGetProperty("error", out var error))
                        throw new InvalidOperationException($"Codex app server error: {error}");
                    return document;
                }
            }
            catch (JsonException)
            {
                // Ignore non-protocol output emitted during startup.
            }
            finally
            {
                if (document is not null &&
                    (!document.RootElement.TryGetProperty("id", out var responseId) || responseId.GetInt32() != id))
                    document.Dispose();
            }
        }
    }

    private static List<string> ReadEfforts(JsonElement item)
    {
        var result = new List<string>();
        if (!item.TryGetProperty("supportedReasoningEfforts", out var efforts) || efforts.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var effort in efforts.EnumerateArray())
        {
            var value = effort.ValueKind == JsonValueKind.String
                ? effort.GetString()
                : effort.ValueKind == JsonValueKind.Object ? GetString(effort, "reasoningEffort") ?? GetString(effort, "effort") : null;
            if (!string.IsNullOrWhiteSpace(value) && !result.Contains(value, StringComparer.OrdinalIgnoreCase))
                result.Add(value);
        }
        return result;
    }

    private static string? ReadUpgrade(JsonElement item)
    {
        if (!item.TryGetProperty("upgrade", out var upgrade)) return null;
        return upgrade.ValueKind == JsonValueKind.String
            ? upgrade.GetString()
            : upgrade.ValueKind == JsonValueKind.Object ? GetString(upgrade, "model") ?? GetString(upgrade, "id") : null;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool GetBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static CodexModelCatalogResult BuildFallback(string message)
    {
        var efforts = CodexAiDefaults.ReasoningEfforts;
        CodexModelCatalogItem Item(string id, string name) => new(id, name, CodexAiDefaults.ReasoningEffort, efforts, id == CodexAiDefaults.Model, null);
        return new CodexModelCatalogResult(
            [Item("gpt-5.6-sol", "GPT-5.6 Sol"), Item("gpt-5.6-terra", "GPT-5.6 Terra"), Item("gpt-5.6-luna", "GPT-5.6 Luna"), Item("gpt-5.5", "GPT-5.5")],
            false,
            message);
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
    }
}
