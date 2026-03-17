using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public class OpenClawSettings
{
    public string CliPath { get; set; } = "openclaw";
    public string BugScanAgentId { get; set; } = "main";
    public List<string> BugScanAgentIds { get; set; } = [];
    public string BugFixAgentId { get; set; } = string.Empty;
    public List<string> BugFixAgentIds { get; set; } = [];
    public string FeatureImplementAgentId { get; set; } = string.Empty;
    public List<string> FeatureImplementAgentIds { get; set; } = [];
    public string BugScanPromptAdditionalInstructions { get; set; } = string.Empty;
    public List<string> BugScanPromptAdditionalInstructionLines { get; set; } = [];
    public string BugFixPromptAdditionalInstructions { get; set; } = string.Empty;
    public List<string> BugFixPromptAdditionalInstructionLines { get; set; } = [];
    public string FeatureImplementPromptAdditionalInstructions { get; set; } = string.Empty;
    public List<string> FeatureImplementPromptAdditionalInstructionLines { get; set; } = [];
    public string FeatureScreenshotImportDirectory { get; set; } = string.Empty;
    public List<string> FeatureScreenshotImportDirectories { get; set; } = [];
    public int FeatureScreenshotImportLookbackMinutes { get; set; } = 180;
    public int FeatureScreenshotImportMaxFiles { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxFindingsPerScan { get; set; } = 8;
}

public sealed class OpenClawBugScanService : IOpenClawBugScanService
{
    private const int DefaultTimeoutSeconds = 120;
    private const int DefaultFindingsPerScan = 8;
    private const int HardMaxFindings = 20;

    private readonly OpenClawSettings _settings;
    private readonly ILogger<OpenClawBugScanService> _logger;

    public OpenClawBugScanService(
        IOptions<OpenClawSettings> settings,
        ILogger<OpenClawBugScanService> logger)
    {
        _settings = settings.Value ?? new OpenClawSettings();
        _logger = logger;
    }

    public int MaxFindingsPerScan => NormalizeFindingsLimit(_settings.MaxFindingsPerScan);

    public async Task<OpenClawBugScanResult> ScanProjectAsync(
        ChangeRequest project,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var configuredCliPath = string.IsNullOrWhiteSpace(_settings.CliPath)
            ? "openclaw"
            : _settings.CliPath.Trim();
        var agentId = ResolveRequestedAgentId(scanAgentId, _settings);
        var timeoutSeconds = NormalizeTimeout(_settings.TimeoutSeconds);
        var findingsLimit = MaxFindingsPerScan;
        var additionalInstructions = ResolveAdditionalInstructions(_settings);
        var prompt = BuildPrompt(project, findingsLimit, additionalInstructions);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set OpenClaw:CliPath to your OpenClaw executable path."
                : $"Set OpenClaw:CliPath to '{recommendedPath}'.";
            return Failed($"OpenClaw scan failed: OpenClaw CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, agentId, prompt, timeoutSeconds);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
                return Failed("OpenClaw scan failed: unable to start OpenClaw process.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start OpenClaw process from path {CliPath}", resolvedCliPath);
            return Failed($"OpenClaw scan failed: cannot start '{resolvedCliPath}'.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds + 20));
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return Failed($"OpenClaw scan timed out after {timeoutSeconds} seconds.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "OpenClaw process exited with code {ExitCode}. stderr: {StdErr}",
                process.ExitCode,
                TrimTo(stderr, 400));

            var errorDetails = !string.IsNullOrWhiteSpace(stderr)
                ? TrimTo(stderr, 220)
                : "OpenClaw command returned a non-zero exit code.";
            return Failed($"OpenClaw scan failed: {errorDetails}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return Failed("OpenClaw scan failed: command returned empty output.");

        if (!TryParseOpenClawResponse(stdout, out var agentText, out var responseError))
        {
            var directFindings = ParseFindingsFromRawCandidates(stdout, findingsLimit);
            if (directFindings.Count == 0 && !string.IsNullOrWhiteSpace(stderr))
                directFindings = ParseFindingsFromRawCandidates(stderr, findingsLimit);
            if (directFindings.Count == 0 && !string.IsNullOrWhiteSpace(stderr))
                directFindings = ParseFindingsFromRawCandidates($"{stdout}\n{stderr}", findingsLimit);

            if (directFindings.Count > 0)
            {
                _logger.LogInformation(
                    "OpenClaw response envelope parse failed but direct finding extraction succeeded ({Count} findings).",
                    directFindings.Count);
                return new OpenClawBugScanResult(true, string.Empty, directFindings);
            }

            _logger.LogWarning(
                "Unable to parse OpenClaw response. stdout: {StdOut}; stderr: {StdErr}",
                TrimTo(stdout, 400),
                TrimTo(stderr, 400));
            var preview = TrimTo(StripAnsi(stdout), 180);
            var previewMessage = string.IsNullOrWhiteSpace(preview) ? string.Empty : $" Output preview: {preview}";
            return Failed($"OpenClaw scan failed: {responseError}.{previewMessage}");
        }

        if (string.IsNullOrWhiteSpace(agentText))
            return new OpenClawBugScanResult(true, string.Empty, []);

        var findings = ParseFindings(agentText, findingsLimit);
        if (findings.Count == 0)
        {
            _logger.LogInformation(
                "OpenClaw scan returned no parseable findings for project {CrNumber}.",
                project.CrNumber);
            return new OpenClawBugScanResult(true, string.Empty, []);
        }

        return new OpenClawBugScanResult(true, string.Empty, findings);
    }

    public async Task<OpenClawBugFixResult> FixBugAsync(
        BugReport bug,
        string? fixAgentId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bug);

        var configuredCliPath = string.IsNullOrWhiteSpace(_settings.CliPath)
            ? "openclaw"
            : _settings.CliPath.Trim();
        var agentId = ResolveRequestedFixAgentId(fixAgentId, _settings);
        var timeoutSeconds = NormalizeTimeout(_settings.TimeoutSeconds);
        var additionalInstructions = ResolveFixAdditionalInstructions(_settings);
        var prompt = BuildFixPrompt(bug, additionalInstructions);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set OpenClaw:CliPath to your OpenClaw executable path."
                : $"Set OpenClaw:CliPath to '{recommendedPath}'.";
            return FailedFix($"OpenClaw fix failed: OpenClaw CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, agentId, prompt, timeoutSeconds);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
                return FailedFix("OpenClaw fix failed: unable to start OpenClaw process.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start OpenClaw fix process from path {CliPath}", resolvedCliPath);
            return FailedFix($"OpenClaw fix failed: cannot start '{resolvedCliPath}'.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds + 20));
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return FailedFix($"OpenClaw fix timed out after {timeoutSeconds} seconds.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            var errorDetails = !string.IsNullOrWhiteSpace(stderr)
                ? TrimTo(stderr, 220)
                : "OpenClaw command returned a non-zero exit code.";
            return FailedFix($"OpenClaw fix failed: {errorDetails}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return FailedFix("OpenClaw fix failed: command returned empty output.");

        string fixPlan;
        if (TryParseOpenClawResponse(stdout, out var agentText, out _))
        {
            fixPlan = agentText;
        }
        else
        {
            fixPlan = StripAnsi(stdout).Trim();
        }

        if (string.IsNullOrWhiteSpace(fixPlan) && !string.IsNullOrWhiteSpace(stderr))
            fixPlan = StripAnsi(stderr).Trim();

        if (string.IsNullOrWhiteSpace(fixPlan))
            return FailedFix("OpenClaw fix failed: empty fix response.");

        return new OpenClawBugFixResult(true, string.Empty, TrimTo(fixPlan, 3500));
    }

    public async Task<OpenClawFeatureImplementResult> ImplementFeatureAsync(
        ProjectFeature feature,
        ChangeRequest? project = null,
        string? featureAgentId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feature);

        var configuredCliPath = string.IsNullOrWhiteSpace(_settings.CliPath)
            ? "openclaw"
            : _settings.CliPath.Trim();
        var agentId = ResolveRequestedFeatureAgentId(featureAgentId, _settings);
        var timeoutSeconds = NormalizeTimeout(_settings.TimeoutSeconds);
        var additionalInstructions = ResolveFeatureAdditionalInstructions(_settings);
        var prompt = BuildFeaturePrompt(feature, project, additionalInstructions);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set OpenClaw:CliPath to your OpenClaw executable path."
                : $"Set OpenClaw:CliPath to '{recommendedPath}'.";
            return FailedFeature($"OpenClaw feature run failed: OpenClaw CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, agentId, prompt, timeoutSeconds);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
                return FailedFeature("OpenClaw feature run failed: unable to start OpenClaw process.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start OpenClaw feature process from path {CliPath}", resolvedCliPath);
            return FailedFeature($"OpenClaw feature run failed: cannot start '{resolvedCliPath}'.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds + 20));
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return FailedFeature($"OpenClaw feature run timed out after {timeoutSeconds} seconds.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            var errorDetails = !string.IsNullOrWhiteSpace(stderr)
                ? TrimTo(stderr, 220)
                : "OpenClaw command returned a non-zero exit code.";
            return FailedFeature($"OpenClaw feature run failed: {errorDetails}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return FailedFeature("OpenClaw feature run failed: command returned empty output.");

        string implementationPlan;
        if (TryParseOpenClawResponse(stdout, out var agentText, out _))
        {
            implementationPlan = agentText;
        }
        else
        {
            implementationPlan = StripAnsi(stdout).Trim();
        }

        if (string.IsNullOrWhiteSpace(implementationPlan) && !string.IsNullOrWhiteSpace(stderr))
            implementationPlan = StripAnsi(stderr).Trim();

        if (string.IsNullOrWhiteSpace(implementationPlan))
            return FailedFeature("OpenClaw feature run failed: empty response.");

        return new OpenClawFeatureImplementResult(true, string.Empty, TrimTo(implementationPlan, 3500));
    }

    private static int NormalizeTimeout(int configuredTimeoutSeconds)
    {
        if (configuredTimeoutSeconds <= 0)
            return DefaultTimeoutSeconds;

        return Math.Clamp(configuredTimeoutSeconds, 10, 900);
    }

    private static string ResolveAdditionalInstructions(OpenClawSettings settings)
    {
        var instructions = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.BugScanPromptAdditionalInstructions))
            instructions.Add(settings.BugScanPromptAdditionalInstructions.Trim());

        if (settings.BugScanPromptAdditionalInstructionLines is not null)
        {
            instructions.AddRange(settings.BugScanPromptAdditionalInstructionLines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim()));
        }

        return string.Join("\n", instructions);
    }

    private static string ResolveFixAdditionalInstructions(OpenClawSettings settings)
    {
        var instructions = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.BugFixPromptAdditionalInstructions))
            instructions.Add(settings.BugFixPromptAdditionalInstructions.Trim());

        if (settings.BugFixPromptAdditionalInstructionLines is not null)
        {
            instructions.AddRange(settings.BugFixPromptAdditionalInstructionLines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim()));
        }

        return string.Join("\n", instructions);
    }

    private static string ResolveFeatureAdditionalInstructions(OpenClawSettings settings)
    {
        var instructions = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.FeatureImplementPromptAdditionalInstructions))
            instructions.Add(settings.FeatureImplementPromptAdditionalInstructions.Trim());

        if (settings.FeatureImplementPromptAdditionalInstructionLines is not null)
        {
            instructions.AddRange(settings.FeatureImplementPromptAdditionalInstructionLines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim()));
        }

        return string.Join("\n", instructions);
    }

    private static string ResolveRequestedAgentId(string? scanAgentId, OpenClawSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(scanAgentId))
            return scanAgentId.Trim();

        var configured = GetConfiguredAgentIds(settings);
        if (configured.Count > 0)
            return configured[0];

        return "main";
    }

    private static string ResolveRequestedFixAgentId(string? fixAgentId, OpenClawSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(fixAgentId))
            return fixAgentId.Trim();

        var configured = GetConfiguredFixAgentIds(settings);
        if (configured.Count > 0)
            return configured[0];

        return "main";
    }

    private static string ResolveRequestedFeatureAgentId(string? featureAgentId, OpenClawSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(featureAgentId))
            return featureAgentId.Trim();

        var configured = GetConfiguredFeatureAgentIds(settings);
        if (configured.Count > 0)
            return configured[0];

        return "main";
    }

    public static IReadOnlyList<string> GetConfiguredAgentIds(OpenClawSettings settings)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static IEnumerable<string> Expand(string value)
        {
            return value
                .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(v => !string.IsNullOrWhiteSpace(v));
        }

        if (!string.IsNullOrWhiteSpace(settings.BugScanAgentId))
        {
            foreach (var id in Expand(settings.BugScanAgentId))
            {
                if (seen.Add(id))
                    result.Add(id);
            }
        }

        if (settings.BugScanAgentIds is not null)
        {
            foreach (var raw in settings.BugScanAgentIds)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                foreach (var id in Expand(raw))
                {
                    if (seen.Add(id))
                        result.Add(id);
                }
            }
        }

        return result;
    }

    public static IReadOnlyList<string> GetConfiguredFixAgentIds(OpenClawSettings settings)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static IEnumerable<string> Expand(string value)
        {
            return value
                .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(v => !string.IsNullOrWhiteSpace(v));
        }

        if (!string.IsNullOrWhiteSpace(settings.BugFixAgentId))
        {
            foreach (var id in Expand(settings.BugFixAgentId))
            {
                if (seen.Add(id))
                    result.Add(id);
            }
        }

        if (settings.BugFixAgentIds is not null)
        {
            foreach (var raw in settings.BugFixAgentIds)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                foreach (var id in Expand(raw))
                {
                    if (seen.Add(id))
                        result.Add(id);
                }
            }
        }

        if (result.Count == 0)
        {
            foreach (var id in GetConfiguredAgentIds(settings))
            {
                if (seen.Add(id))
                    result.Add(id);
            }
        }

        if (result.Count == 0)
            result.Add("main");

        return result;
    }

    public static IReadOnlyList<string> GetConfiguredFeatureAgentIds(OpenClawSettings settings)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static IEnumerable<string> Expand(string value)
        {
            return value
                .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(v => !string.IsNullOrWhiteSpace(v));
        }

        if (!string.IsNullOrWhiteSpace(settings.FeatureImplementAgentId))
        {
            foreach (var id in Expand(settings.FeatureImplementAgentId))
            {
                if (seen.Add(id))
                    result.Add(id);
            }
        }

        if (settings.FeatureImplementAgentIds is not null)
        {
            foreach (var raw in settings.FeatureImplementAgentIds)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                foreach (var id in Expand(raw))
                {
                    if (seen.Add(id))
                        result.Add(id);
                }
            }
        }

        if (result.Count == 0)
        {
            foreach (var id in GetConfiguredFixAgentIds(settings))
            {
                if (seen.Add(id))
                    result.Add(id);
            }
        }

        if (result.Count == 0)
            result.Add("main");

        return result;
    }

    private static ProcessStartInfo BuildProcessStartInfo(
        string resolvedCliPath,
        string agentId,
        string prompt,
        int timeoutSeconds)
    {
        var args = new[]
        {
            "agent",
            "--agent",
            agentId,
            "--message",
            prompt,
            "--json",
            "--timeout",
            timeoutSeconds.ToString(CultureInfo.InvariantCulture)
        };

        var usePowerShellHost = resolvedCliPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo
        {
            FileName = usePowerShellHost ? "powershell" : resolvedCliPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (usePowerShellHost)
        {
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(resolvedCliPath);
        }

        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        return startInfo;
    }

    private static string? ResolveCliExecutable(string configuredCliPath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defaultCommand = string.IsNullOrWhiteSpace(configuredCliPath)
            ? "openclaw"
            : configuredCliPath.Trim();

        var explicitCommandFallback = defaultCommand;
        foreach (var candidate in EnumerateCliCandidates(defaultCommand))
        {
            var normalizedCandidate = NormalizePathCandidate(candidate);
            if (string.IsNullOrWhiteSpace(normalizedCandidate))
                continue;

            if (!seen.Add(normalizedCandidate))
                continue;

            // Keep non-rooted command candidate as fallback if no explicit file path resolves.
            if (!Path.IsPathRooted(normalizedCandidate) &&
                !normalizedCandidate.Contains(Path.DirectorySeparatorChar) &&
                !normalizedCandidate.Contains(Path.AltDirectorySeparatorChar))
            {
                explicitCommandFallback = normalizedCandidate;
                continue;
            }

            if (File.Exists(normalizedCandidate))
                return normalizedCandidate;
        }

        return explicitCommandFallback;
    }

    private static IEnumerable<string> EnumerateCliCandidates(string configuredCliPath)
    {
        var envCliPath = Environment.GetEnvironmentVariable("OPENCLAW_CLI_PATH")?.Trim();
        if (!string.IsNullOrWhiteSpace(envCliPath))
            yield return envCliPath;

        yield return configuredCliPath;

        if (!OperatingSystem.IsWindows())
            yield break;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrWhiteSpace(appData))
        {
            yield return Path.Combine(appData, "npm", "openclaw.cmd");
            yield return Path.Combine(appData, "npm", "openclaw.ps1");
            yield return Path.Combine(appData, "npm", "openclaw.exe");
        }

        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return Path.Combine(userProfile, "AppData", "Roaming", "npm", "openclaw.cmd");
            yield return Path.Combine(userProfile, "AppData", "Roaming", "npm", "openclaw.ps1");
        }

        if (!string.IsNullOrWhiteSpace(localAppData))
            yield return Path.Combine(localAppData, "pnpm", "openclaw.cmd");
    }

    private static string NormalizePathCandidate(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return string.Empty;

        return Environment.ExpandEnvironmentVariables(candidate.Trim());
    }

    private static string GetRecommendedWindowsCliPath()
    {
        if (!OperatingSystem.IsWindows())
            return string.Empty;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
            return string.Empty;

        var path = Path.Combine(appData, "npm", "openclaw.cmd");
        return File.Exists(path) ? path : string.Empty;
    }

    private static int NormalizeFindingsLimit(int configuredLimit)
    {
        if (configuredLimit <= 0)
            return DefaultFindingsPerScan;

        return Math.Clamp(configuredLimit, 1, HardMaxFindings);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Ignore kill errors.
        }
    }

    private static bool TryParseOpenClawResponse(
        string rawOutput,
        out string responseText,
        out string error)
    {
        responseText = string.Empty;
        error = "unable to parse command output.";

        if (!TryParseJsonDocument(rawOutput, out var json))
            return false;

        using (json)
        {
            var root = json.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                responseText = root.GetRawText();
                return true;
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("findings", out var findingsElement) &&
                findingsElement.ValueKind == JsonValueKind.Array)
            {
                responseText = root.GetRawText();
                return true;
            }

            var status = root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString()
                : null;

            if (!string.Equals(status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                var summary = root.TryGetProperty("summary", out var summaryElement)
                    ? summaryElement.GetString()
                    : null;
                error = string.IsNullOrWhiteSpace(summary)
                    ? "agent status was not ok."
                    : summary.Trim();
                return false;
            }

            if (!root.TryGetProperty("result", out var resultElement) ||
                !resultElement.TryGetProperty("payloads", out var payloadsElement) ||
                payloadsElement.ValueKind != JsonValueKind.Array)
            {
                responseText = string.Empty;
                return true;
            }

            var sb = new StringBuilder();
            foreach (var payload in payloadsElement.EnumerateArray())
            {
                if (!payload.TryGetProperty("text", out var textElement))
                    continue;

                var text = textElement.GetString();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (sb.Length > 0)
                    sb.AppendLine();

                sb.Append(text.Trim());
            }

            responseText = sb.ToString().Trim();
            return true;
        }
    }

    private static List<OpenClawBugFinding> ParseFindings(string agentText, int maxFindings)
    {
        var cleaned = StripCodeFence(agentText);
        if (!TryParseJsonDocument(cleaned, out var json))
            return [];

        using (json)
        {
            var root = json.RootElement;

            JsonElement findingsElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                findingsElement = root;
            }
            else if (root.ValueKind == JsonValueKind.Object &&
                     root.TryGetProperty("findings", out var findingsProperty) &&
                     findingsProperty.ValueKind == JsonValueKind.Array)
            {
                findingsElement = findingsProperty;
            }
            else
            {
                return [];
            }

            var results = new List<OpenClawBugFinding>();
            var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var findingElement in findingsElement.EnumerateArray())
            {
                if (findingElement.ValueKind != JsonValueKind.Object)
                    continue;

                var title = TrimTo(ReadString(findingElement, "title"), 300);
                if (string.IsNullOrWhiteSpace(title) || !seenTitles.Add(title))
                    continue;

                var description = TrimTo(ReadString(findingElement, "description"), 4000);
                var workflow = TrimTo(ReadString(findingElement, "workflow"), 4000);
                var steps = TrimTo(ReadString(findingElement, "stepsToReproduce"), 4000);
                var module = TrimTo(ReadString(findingElement, "moduleImpacted"), 200);

                if (string.IsNullOrWhiteSpace(description))
                    description = "Generated by OpenClaw scan.";
                if (string.IsNullOrWhiteSpace(workflow))
                    workflow = "Review project flow and run targeted validation for this issue.";
                if (string.IsNullOrWhiteSpace(steps))
                    steps = "1. Open the impacted flow.\n2. Execute the scenario.\n3. Validate expected behavior.";
                if (string.IsNullOrWhiteSpace(module))
                    module = "General";

                results.Add(new OpenClawBugFinding(
                    title,
                    description,
                    workflow,
                    steps,
                    module));

                if (results.Count >= maxFindings)
                    break;
            }

            return results;
        }
    }

    private static List<OpenClawBugFinding> ParseFindingsFromRawCandidates(string raw, int maxFindings)
    {
        var candidates = new List<string>();
        var normalized = StripAnsi(raw).Trim();
        if (!string.IsNullOrWhiteSpace(normalized))
            candidates.Add(normalized);

        var strippedFence = StripCodeFence(normalized);
        if (!string.IsNullOrWhiteSpace(strippedFence) &&
            !string.Equals(strippedFence, normalized, StringComparison.Ordinal))
        {
            candidates.Add(strippedFence);
        }

        foreach (var candidate in ExtractJsonCandidates(normalized))
            candidates.Add(candidate);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !seen.Add(candidate))
                continue;

            var findings = ParseFindings(candidate, maxFindings);
            if (findings.Count > 0)
                return findings;
        }

        return [];
    }

    private static string BuildPrompt(ChangeRequest project, int findingsLimit, string additionalInstructions)
    {
        var features = project.Features
            .Select(f => f.Name?.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Take(10)
            .ToList();

        var repositoryFeatures = project.RepositoryFeatures
            .Select(f => f.Name?.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Take(10)
            .ToList();

        var description = TrimTo(project.Description, 1200);
        var tech = TrimTo(project.TechnologyStack, 400);

        var sb = new StringBuilder();
        sb.AppendLine("You are a software QA bug triage assistant.");
        sb.AppendLine("Return only JSON, no markdown and no extra text.");
        sb.AppendLine("Schema:");
        sb.AppendLine("{\"findings\":[{\"title\":\"...\",\"description\":\"...\",\"workflow\":\"...\",\"stepsToReproduce\":\"...\",\"moduleImpacted\":\"...\"}]}");
        sb.AppendLine();
        sb.AppendLine($"Generate up to {findingsLimit} high-confidence bugs.");
        sb.AppendLine("Focus on actionable software defects, not vague suggestions.");
        sb.AppendLine();
        sb.AppendLine($"Project Number: {project.CrNumber}");
        sb.AppendLine($"Project Title: {TrimTo(project.Title, 300)}");
        sb.AppendLine($"Project Stage: {project.Stage}");
        sb.AppendLine($"Project Status: {project.Status}");
        if (!string.IsNullOrWhiteSpace(description))
            sb.AppendLine($"Description: {description}");
        if (!string.IsNullOrWhiteSpace(tech))
            sb.AppendLine($"Technology Stack: {tech}");
        if (features.Count > 0)
            sb.AppendLine($"Project Features: {string.Join(", ", features)}");
        if (repositoryFeatures.Count > 0)
            sb.AppendLine($"Repository Scan Features: {string.Join(", ", repositoryFeatures)}");

        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Every finding must include all schema fields.");
        sb.AppendLine("- Keep title under 120 characters.");
        sb.AppendLine("- Keep moduleImpacted short and specific.");
        if (!string.IsNullOrWhiteSpace(additionalInstructions))
        {
            sb.AppendLine("- Follow additional project-specific instructions below.");
            sb.AppendLine("Additional instructions:");
            sb.AppendLine(additionalInstructions);
        }

        var raw = TrimTo(sb.ToString(), 5000);
        // openclaw.cmd can lose multi-line argument content on Windows; send one-line prompt.
        var singleLine = Regex.Replace(raw, @"\s+", " ").Trim();
        return singleLine;
    }

    private static string BuildFixPrompt(BugReport bug, string additionalInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a senior software engineer fixing a bug ticket.");
        sb.AppendLine("Return practical fix guidance that a developer can implement immediately.");
        sb.AppendLine("Keep response structured with sections:");
        sb.AppendLine("1) Root cause");
        sb.AppendLine("2) Proposed code changes");
        sb.AppendLine("3) Validation tests");
        sb.AppendLine("4) Risks");
        sb.AppendLine();
        sb.AppendLine($"Bug Number: {bug.BugNumber}");
        sb.AppendLine($"Title: {TrimTo(bug.Title, 300)}");
        if (!string.IsNullOrWhiteSpace(bug.Description))
            sb.AppendLine($"Description: {TrimTo(bug.Description, 1400)}");
        if (!string.IsNullOrWhiteSpace(bug.Workflow))
            sb.AppendLine($"Workflow: {TrimTo(bug.Workflow, 1200)}");
        if (!string.IsNullOrWhiteSpace(bug.StepsToReproduce))
            sb.AppendLine($"Steps To Reproduce: {TrimTo(bug.StepsToReproduce, 1200)}");
        if (!string.IsNullOrWhiteSpace(bug.ModuleImpacted))
            sb.AppendLine($"Module Impacted: {TrimTo(bug.ModuleImpacted, 200)}");
        if (!string.IsNullOrWhiteSpace(bug.ChangeRequestReferenceText))
            sb.AppendLine($"Project Reference: {TrimTo(bug.ChangeRequestReferenceText, 300)}");

        sb.AppendLine();
        sb.AppendLine("Constraints:");
        sb.AppendLine("- Prefer the smallest safe fix first.");
        sb.AppendLine("- Include concrete checks for regression.");
        sb.AppendLine("- If information is missing, clearly list assumptions.");
        if (!string.IsNullOrWhiteSpace(additionalInstructions))
        {
            sb.AppendLine("- Follow additional project-specific instructions below.");
            sb.AppendLine("Additional instructions:");
            sb.AppendLine(additionalInstructions);
        }

        var raw = TrimTo(sb.ToString(), 5000);
        return Regex.Replace(raw, @"\s+", " ").Trim();
    }

    private static string BuildFeaturePrompt(ProjectFeature feature, ChangeRequest? project, string additionalInstructions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a senior software engineer implementing a feature ticket.");
        sb.AppendLine("Return practical implementation guidance with these sections:");
        sb.AppendLine("1) Implementation approach");
        sb.AppendLine("2) Files/components likely to change");
        sb.AppendLine("3) Validation checklist");
        sb.AppendLine("4) Risks and rollback notes");
        sb.AppendLine();
        sb.AppendLine($"Feature Number: {feature.FeatureNumber}");
        sb.AppendLine($"Feature Title: {TrimTo(feature.Name, 300)}");
        sb.AppendLine($"Status: {feature.Status}");
        sb.AppendLine($"Priority: {feature.Priority}");
        sb.AppendLine($"Stage: {feature.Stage}");
        if (!string.IsNullOrWhiteSpace(feature.Description))
            sb.AppendLine($"Description: {TrimTo(feature.Description, 1400)}");
        if (!string.IsNullOrWhiteSpace(feature.ModuleImpacted))
            sb.AppendLine($"Affected Module: {TrimTo(feature.ModuleImpacted, 200)}");
        if (!string.IsNullOrWhiteSpace(feature.LinkedBugs))
            sb.AppendLine($"Linked Bugs: {TrimTo(feature.LinkedBugs, 600)}");
        if (feature.TimelineStart.HasValue || feature.TimelineEnd.HasValue)
            sb.AppendLine($"Timeline: {feature.TimelineStart:yyyy-MM-dd} to {feature.TimelineEnd:yyyy-MM-dd}");

        if (project is not null)
        {
            sb.AppendLine($"Project Number: {project.CrNumber}");
            sb.AppendLine($"Project Title: {TrimTo(project.Title, 300)}");
            if (!string.IsNullOrWhiteSpace(project.Description))
                sb.AppendLine($"Project Description: {TrimTo(project.Description, 1200)}");
            if (!string.IsNullOrWhiteSpace(project.TechnologyStack))
                sb.AppendLine($"Technology Stack: {TrimTo(project.TechnologyStack, 500)}");
            if (!string.IsNullOrWhiteSpace(project.GitHubRepoUrl))
                sb.AppendLine($"Repository URL: {TrimTo(project.GitHubRepoUrl, 500)}");
            if (!string.IsNullOrWhiteSpace(project.GitHubBranch))
                sb.AppendLine($"Repository Branch: {TrimTo(project.GitHubBranch, 100)}");
        }

        sb.AppendLine();
        sb.AppendLine("Constraints:");
        sb.AppendLine("- Prefer the smallest safe implementation first.");
        sb.AppendLine("- Provide clear step-by-step tasks suitable for an assignee.");
        sb.AppendLine("- Include explicit verification checks.");
        sb.AppendLine("- If you captured UI screenshots, include absolute image file paths in the response.");
        sb.AppendLine("- Add a line exactly like: SCREENSHOT_PATHS: path1.png | path2.png");
        if (!string.IsNullOrWhiteSpace(additionalInstructions))
        {
            sb.AppendLine("- Follow additional project-specific instructions below.");
            sb.AppendLine("Additional instructions:");
            sb.AppendLine(additionalInstructions);
        }

        var raw = TrimTo(sb.ToString(), 5000);
        return Regex.Replace(raw, @"\s+", " ").Trim();
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var propertyElement))
            return string.Empty;

        return propertyElement.ValueKind == JsonValueKind.String
            ? propertyElement.GetString() ?? string.Empty
            : propertyElement.ToString();
    }

    private static bool TryParseJsonDocument(string value, out JsonDocument document)
    {
        var trimmed = StripAnsi(value).Trim();
        if (TryParseJson(trimmed, out document))
            return true;

        var lines = trimmed
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (TryParseJson(lines[i], out document))
                return true;
        }

        foreach (var candidate in ExtractJsonCandidates(trimmed))
        {
            if (TryParseJson(candidate, out document))
                return true;
        }

        var firstObject = trimmed.IndexOf('{');
        var lastObject = trimmed.LastIndexOf('}');
        if (firstObject >= 0 && lastObject > firstObject)
        {
            var objectSlice = trimmed[firstObject..(lastObject + 1)];
            if (TryParseJson(objectSlice, out document))
                return true;
        }

        var firstArray = trimmed.IndexOf('[');
        var lastArray = trimmed.LastIndexOf(']');
        if (firstArray >= 0 && lastArray > firstArray)
        {
            var arraySlice = trimmed[firstArray..(lastArray + 1)];
            if (TryParseJson(arraySlice, out document))
                return true;
        }

        document = null!;
        return false;
    }

    private static bool TryParseJson(string value, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(value);
            return true;
        }
        catch
        {
            document = null!;
            return false;
        }
    }

    private static IEnumerable<string> ExtractJsonCandidates(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        var spans = new List<(int Start, int End)>();
        var stack = new Stack<(char Open, int Start)>();
        var inString = false;
        var escaped = false;

        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (ch == '"')
                    inString = false;

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch is '{' or '[')
            {
                stack.Push((ch, i));
                continue;
            }

            if (ch is '}' or ']')
            {
                if (stack.Count == 0)
                    continue;

                var current = stack.Pop();
                var matches = (current.Open == '{' && ch == '}') || (current.Open == '[' && ch == ']');
                if (!matches)
                {
                    stack.Clear();
                    continue;
                }

                if (stack.Count == 0)
                    spans.Add((current.Start, i));
            }
        }

        for (var i = spans.Count - 1; i >= 0; i--)
        {
            var span = spans[i];
            yield return value[span.Start..(span.End + 1)];
        }
    }

    private static string StripAnsi(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var input = value!;
        var sb = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch != '\u001b')
            {
                sb.Append(ch);
                continue;
            }

            // Skip CSI escape sequence: ESC [ ... command
            if (i + 1 < input.Length && input[i + 1] == '[')
            {
                i += 2;
                while (i < input.Length)
                {
                    var c = input[i];
                    if (c is >= '@' and <= '~')
                        break;
                    i++;
                }
            }
        }

        return sb.ToString();
    }

    private static string StripCodeFence(string value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var lines = trimmed.Split('\n');
        if (lines.Length <= 2)
            return trimmed;

        var body = lines.Skip(1).ToArray();
        if (body.Length > 0 && body[^1].TrimStart().StartsWith("```", StringComparison.Ordinal))
            body = body[..^1];

        return string.Join('\n', body).Trim();
    }

    private static string TrimTo(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length <= maxLength)
            return text;

        return text[..maxLength].Trim();
    }

    private static OpenClawBugScanResult Failed(string error) =>
        new(false, error, []);

    private static OpenClawBugFixResult FailedFix(string error) =>
        new(false, error, string.Empty);

    private static OpenClawFeatureImplementResult FailedFeature(string error) =>
        new(false, error, string.Empty);
}
