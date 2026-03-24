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
    public string GatewayUrl { get; set; } = string.Empty;
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

    // Prompt templates — use {findingsLimit}, {projectNumber}, {projectTitle}, {projectStage},
    // {projectStatus}, {description}, {techStack}, {features}, {repositoryFeatures} placeholders.
    public string BugScanSystemRole { get; set; } = "You are a software QA bug triage assistant.";
    public string BugScanOutputFormat { get; set; } = "Return only JSON, no markdown and no extra text.";
    public string BugScanSchema { get; set; } = "{\"findings\":[{\"title\":\"...\",\"description\":\"...\",\"workflow\":\"...\",\"stepsToReproduce\":\"...\",\"moduleImpacted\":\"...\",\"screenshotPaths\":[\"C:\\\\path\\\\shot1.png\"]}]}";
    public string BugScanInstruction { get; set; } = "Generate up to {findingsLimit} high-confidence bugs.";
    public string BugScanFocus { get; set; } = "Focus on actionable software defects, not vague suggestions.";
    public List<string> BugScanRules { get; set; } =
    [
        "Every finding must include all schema fields.",
        "Keep title under 120 characters.",
        "Keep moduleImpacted short and specific.",
        "If screenshot evidence exists, include absolute local image file paths in screenshotPaths.",
        "If no screenshot exists for a finding, set screenshotPaths to an empty array."
    ];

    // Module scan — uses same schema; additional placeholders: {moduleName}
    public string ModuleScanInstruction { get; set; } = "Generate up to {findingsLimit} high-confidence bugs SPECIFICALLY for the module: {moduleName}";
    public string ModuleScanFocus { get; set; } = "Focus ONLY on the \"{moduleName}\" module. Do not scan other modules.";
    public List<string> ModuleScanRules { get; set; } =
    [
        "Every finding must include all schema fields.",
        "Set moduleImpacted to \"{moduleName}\" for all findings.",
        "Keep title under 120 characters.",
        "If screenshot evidence exists, include absolute local image file paths in screenshotPaths.",
        "If no screenshot exists for a finding, set screenshotPaths to an empty array."
    ];

    // Bug fix prompt — uses {bugNumber}, {bugTitle}, {bugDescription}, {bugWorkflow},
    // {bugSteps}, {bugModule}, {bugProjectRef} placeholders.
    public string BugFixSystemRole { get; set; } = "You are a senior software engineer fixing a bug ticket.";
    public string BugFixOutputFormat { get; set; } = "Return practical fix guidance that a developer can implement immediately.";
    public List<string> BugFixSections { get; set; } = ["1) Root cause", "2) Proposed code changes", "3) Validation tests", "4) Risks"];
    public List<string> BugFixConstraints { get; set; } =
    [
        "Prefer the smallest safe fix first.",
        "Include concrete checks for regression.",
        "If information is missing, clearly list assumptions."
    ];

    // Test case auto-generation prompt — uses {projectNumber}, {projectTitle}, {description},
    // {techStack}, {features}, {maxTestCases} placeholders.
    public string TestCaseGenSystemRole { get; set; } = "You are a senior QA engineer generating module-level test cases for a software project.";
    public string TestCaseGenOutputFormat { get; set; } = "Return only JSON, no markdown and no extra text.";
    public string TestCaseGenSchema { get; set; } = "{\"testCases\":[{\"name\":\"...\",\"description\":\"...\",\"module\":\"...\",\"category\":\"Regression\",\"environment\":\"Dev\"}]}";
    public string TestCaseGenInstruction { get; set; } = "Generate up to {maxTestCases} high-level module-based test cases for this project.";
    public string TestCaseGenFocus { get; set; } = "Each test case must cover an entire module or major workflow, NOT small individual unit tests. Think end-to-end scenarios, integration tests, and full-module verification.";
    public List<string> TestCaseGenRules { get; set; } =
    [
        "Every test case must include all schema fields: name, description, module, category, environment.",
        "Each test case should target a distinct module or major feature area.",
        "The name should clearly describe the module-level scenario being tested.",
        "The description should include: purpose, preconditions, key test steps, and expected outcome.",
        "Set module to the specific module or feature area name (e.g. Authentication, Payment, Dashboard).",
        "Category must be one of: Smoke, Regression, UAT.",
        "Environment must be one of: Dev, Staging, UAT, Production.",
        "Do NOT create tiny unit-level test cases. Focus on broad module coverage."
    ];
    public int MaxTestCasesPerGeneration { get; set; } = 10;
    public string TestCaseGenPromptAdditionalInstructions { get; set; } = string.Empty;
    public List<string> TestCaseGenPromptAdditionalInstructionLines { get; set; } = [];

    // Feature implement prompt — uses {featureNumber}, {featureTitle}, {featureStatus},
    // {featurePriority}, {featureStage}, {featureDescription}, {featureModule}, {featureLinkedBugs},
    // {featureTimeline}, {projectNumber}, {projectTitle}, {description}, {techStack},
    // {repoUrl}, {repoBranch} placeholders.
    public string FeatureImplementSystemRole { get; set; } = "You are a senior software engineer implementing a feature ticket.";
    public string FeatureImplementOutputFormat { get; set; } = "Return practical implementation guidance with these sections:";
    public List<string> FeatureImplementSections { get; set; } = ["1) Implementation approach", "2) Files/components likely to change", "3) Validation checklist", "4) Risks and rollback notes"];
    public List<string> FeatureImplementConstraints { get; set; } =
    [
        "Prefer the smallest safe implementation first.",
        "Provide clear step-by-step tasks suitable for an assignee.",
        "Include explicit verification checks.",
        "If you captured UI screenshots, include absolute image file paths in the response.",
        "Add a line exactly like: SCREENSHOT_PATHS: path1.png | path2.png"
    ];
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
        var prompt = BuildPrompt(project, findingsLimit, additionalInstructions, _settings);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set OpenClaw:CliPath to your OpenClaw executable path."
                : $"Set OpenClaw:CliPath to '{recommendedPath}'.";
            return Failed($"OpenClaw scan failed: OpenClaw CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, agentId, prompt, timeoutSeconds, _settings.GatewayUrl);

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
                return new OpenClawBugScanResult(true, string.Empty, directFindings, stdout);
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
            return new OpenClawBugScanResult(true, string.Empty, [], stdout);

        var findings = ParseFindings(agentText, findingsLimit);
        if (findings.Count == 0)
        {
            _logger.LogInformation(
                "OpenClaw scan returned no parseable findings for project {CrNumber}.",
                project.CrNumber);
            return new OpenClawBugScanResult(true, string.Empty, [], stdout);
        }

        return new OpenClawBugScanResult(true, string.Empty, findings, stdout);
    }

    public async Task<OpenClawBugScanResult> ScanModuleAsync(
        ChangeRequest project,
        string moduleName,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var configuredCliPath = string.IsNullOrWhiteSpace(_settings.CliPath)
            ? "openclaw"
            : _settings.CliPath.Trim();
        var agentId = ResolveRequestedAgentId(scanAgentId, _settings);
        var timeoutSeconds = NormalizeTimeout(_settings.TimeoutSeconds);
        var findingsLimit = MaxFindingsPerScan;
        var additionalInstructions = ResolveAdditionalInstructions(_settings);
        var prompt = BuildModuleScanPrompt(project, moduleName.Trim(), findingsLimit, additionalInstructions, _settings);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set OpenClaw:CliPath to your OpenClaw executable path."
                : $"Set OpenClaw:CliPath to '{recommendedPath}'.";
            return Failed($"OpenClaw scan failed: OpenClaw CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, agentId, prompt, timeoutSeconds, _settings.GatewayUrl);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
                return Failed("OpenClaw scan failed: unable to start OpenClaw process.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start OpenClaw module scan process from path {CliPath}", resolvedCliPath);
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
            return Failed($"OpenClaw module scan timed out after {timeoutSeconds} seconds.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "OpenClaw module scan exited with code {ExitCode}. stderr: {StdErr}",
                process.ExitCode, TrimTo(stderr, 400));
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
                    "OpenClaw module scan response parse failed but direct extraction succeeded ({Count} findings).",
                    directFindings.Count);
                return new OpenClawBugScanResult(true, string.Empty, directFindings, stdout);
            }

            var preview = TrimTo(StripAnsi(stdout), 180);
            var previewMessage = string.IsNullOrWhiteSpace(preview) ? string.Empty : $" Output preview: {preview}";
            return Failed($"OpenClaw scan failed: {responseError}.{previewMessage}");
        }

        if (string.IsNullOrWhiteSpace(agentText))
            return new OpenClawBugScanResult(true, string.Empty, [], stdout);

        var findings = ParseFindings(agentText, findingsLimit);
        if (findings.Count == 0)
        {
            _logger.LogInformation(
                "OpenClaw module scan returned no parseable findings for project {CrNumber} module {Module}.",
                project.CrNumber, moduleName);
            return new OpenClawBugScanResult(true, string.Empty, [], stdout);
        }

        return new OpenClawBugScanResult(true, string.Empty, findings, stdout);
    }

    public async Task<OpenClawTestCaseGenResult> GenerateTestCasesAsync(
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
        var maxTestCases = Math.Clamp(_settings.MaxTestCasesPerGeneration, 1, 30);
        var additionalInstructions = ResolveTestCaseGenAdditionalInstructions(_settings);
        var prompt = BuildTestCaseGenPrompt(project, maxTestCases, additionalInstructions, _settings);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set OpenClaw:CliPath to your OpenClaw executable path."
                : $"Set OpenClaw:CliPath to '{recommendedPath}'.";
            return FailedTestCaseGen($"OpenClaw failed: CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, agentId, prompt, timeoutSeconds, _settings.GatewayUrl);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
                return FailedTestCaseGen("OpenClaw failed: unable to start process.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start OpenClaw test case gen process from path {CliPath}", resolvedCliPath);
            return FailedTestCaseGen($"OpenClaw failed: cannot start '{resolvedCliPath}'.");
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
            return FailedTestCaseGen($"OpenClaw test case generation timed out after {timeoutSeconds} seconds.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("OpenClaw test case gen exited with code {ExitCode}. stderr: {StdErr}",
                process.ExitCode, TrimTo(stderr, 400));
            var errorDetails = !string.IsNullOrWhiteSpace(stderr) ? TrimTo(stderr, 220) : "Non-zero exit code.";
            return FailedTestCaseGen($"OpenClaw failed: {errorDetails}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return FailedTestCaseGen("OpenClaw failed: empty output.");

        // Try envelope parse first, then raw
        string? agentText = null;
        if (TryParseOpenClawResponse(stdout, out var envelopeText, out _))
            agentText = envelopeText;
        else
            agentText = stdout;

        if (string.IsNullOrWhiteSpace(agentText))
            return new OpenClawTestCaseGenResult(true, string.Empty, [], stdout);

        var testCases = ParseGeneratedTestCases(agentText, maxTestCases);
        if (testCases.Count == 0)
        {
            _logger.LogInformation("OpenClaw test case gen returned no parseable test cases for project {CrNumber}.", project.CrNumber);
            return new OpenClawTestCaseGenResult(true, string.Empty, [], stdout);
        }

        return new OpenClawTestCaseGenResult(true, string.Empty, testCases, stdout);
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
        var prompt = BuildFixPrompt(bug, additionalInstructions, _settings);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set OpenClaw:CliPath to your OpenClaw executable path."
                : $"Set OpenClaw:CliPath to '{recommendedPath}'.";
            return FailedFix($"OpenClaw fix failed: OpenClaw CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, agentId, prompt, timeoutSeconds, _settings.GatewayUrl);
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
        var prompt = BuildFeaturePrompt(feature, project, additionalInstructions, _settings);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set OpenClaw:CliPath to your OpenClaw executable path."
                : $"Set OpenClaw:CliPath to '{recommendedPath}'.";
            return FailedFeature($"OpenClaw feature run failed: OpenClaw CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, agentId, prompt, timeoutSeconds, _settings.GatewayUrl);
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

    private static string ResolveTestCaseGenAdditionalInstructions(OpenClawSettings settings)
    {
        var instructions = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.TestCaseGenPromptAdditionalInstructions))
            instructions.Add(settings.TestCaseGenPromptAdditionalInstructions.Trim());

        if (settings.TestCaseGenPromptAdditionalInstructionLines is not null)
        {
            instructions.AddRange(settings.TestCaseGenPromptAdditionalInstructionLines
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
        int timeoutSeconds,
        string? configuredGatewayUrl = null)
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

        var gatewayUrl = configuredGatewayUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(gatewayUrl))
        {
            // Keep both names to support different OpenClaw CLI env conventions.
            startInfo.Environment["OPENCLAW_GATEWAY_URL"] = gatewayUrl;
            startInfo.Environment["OPENCLAW_BASE_URL"] = gatewayUrl;
        }

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

            if (TryExtractPayloadText(root, out var directPayloadText))
            {
                responseText = directPayloadText;
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
                !TryExtractPayloadText(resultElement, out var payloadText))
            {
                responseText = string.Empty;
                return true;
            }

            responseText = payloadText;
            return true;
        }
    }

    private static bool TryExtractPayloadText(JsonElement container, out string payloadText)
    {
        payloadText = string.Empty;

        if (!container.TryGetProperty("payloads", out var payloadsElement) ||
            payloadsElement.ValueKind != JsonValueKind.Array)
            return false;

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

        payloadText = sb.ToString().Trim();
        return true;
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
                var severity = NormalizeSeverityLabel(ReadFirstString(
                    findingElement,
                    "severity",
                    "severityLevel",
                    "riskLevel",
                    "risk",
                    "priority"));
                var screenshotPaths = ParseScreenshotPaths(findingElement, description, workflow, steps);

                if (string.IsNullOrWhiteSpace(description))
                    description = "Generated by OpenClaw scan.";
                if (string.IsNullOrWhiteSpace(workflow))
                    workflow = "Review project flow and run targeted validation for this issue.";
                if (string.IsNullOrWhiteSpace(steps))
                    steps = "1. Open the impacted flow.\n2. Execute the scenario.\n3. Validate expected behavior.";
                if (string.IsNullOrWhiteSpace(module))
                    module = "General";
                if (string.IsNullOrWhiteSpace(severity))
                    severity = "Medium";

                results.Add(new OpenClawBugFinding(
                    title,
                    description,
                    workflow,
                    steps,
                    module,
                    severity,
                    screenshotPaths));

                if (results.Count >= maxFindings)
                    break;
            }

            return results;
        }
    }

    private static List<OpenClawGeneratedTestCase> ParseGeneratedTestCases(string agentText, int maxItems)
    {
        var cleaned = StripCodeFence(agentText);
        if (!TryParseJsonDocument(cleaned, out var json))
            return [];

        using (json)
        {
            var root = json.RootElement;

            JsonElement testCasesElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                testCasesElement = root;
            }
            else if (root.ValueKind == JsonValueKind.Object &&
                     root.TryGetProperty("testCases", out var tcProperty) &&
                     tcProperty.ValueKind == JsonValueKind.Array)
            {
                testCasesElement = tcProperty;
            }
            else
            {
                return [];
            }

            var results = new List<OpenClawGeneratedTestCase>();
            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in testCasesElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var name = TrimTo(ReadString(item, "name"), 300);
                if (string.IsNullOrWhiteSpace(name) || !seenNames.Add(name))
                    continue;

                var description = TrimTo(ReadString(item, "description"), 4000);
                var module = TrimTo(ReadString(item, "module"), 200);
                var category = TrimTo(ReadString(item, "category"), 50);
                var environment = TrimTo(ReadString(item, "environment"), 50);

                if (string.IsNullOrWhiteSpace(description))
                    description = $"Module-level test case for {module}.";
                if (string.IsNullOrWhiteSpace(module))
                    module = "General";
                if (string.IsNullOrWhiteSpace(category))
                    category = "Regression";
                if (string.IsNullOrWhiteSpace(environment))
                    environment = "Dev";

                results.Add(new OpenClawGeneratedTestCase(name, description, module, category, environment));

                if (results.Count >= maxItems)
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

    private static string BuildPrompt(ChangeRequest project, int findingsLimit, string additionalInstructions, OpenClawSettings settings)
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
        sb.AppendLine(settings.BugScanSystemRole);
        sb.AppendLine(settings.BugScanOutputFormat);
        sb.AppendLine("Schema:");
        sb.AppendLine(settings.BugScanSchema);
        sb.AppendLine();
        sb.AppendLine(settings.BugScanInstruction.Replace("{findingsLimit}", findingsLimit.ToString()));
        sb.AppendLine(settings.BugScanFocus);
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
        foreach (var rule in settings.BugScanRules)
            sb.AppendLine($"- {rule}");
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

    private static string BuildModuleScanPrompt(
        ChangeRequest project, string moduleName, int findingsLimit, string additionalInstructions, OpenClawSettings settings)
    {
        var description = TrimTo(project.Description, 1200);
        var tech = TrimTo(project.TechnologyStack, 400);

        var sb = new StringBuilder();
        sb.AppendLine(settings.BugScanSystemRole);
        sb.AppendLine(settings.BugScanOutputFormat);
        sb.AppendLine("Schema:");
        sb.AppendLine(settings.BugScanSchema);
        sb.AppendLine();
        sb.AppendLine(settings.ModuleScanInstruction
            .Replace("{findingsLimit}", findingsLimit.ToString())
            .Replace("{moduleName}", moduleName));
        sb.AppendLine(settings.ModuleScanFocus.Replace("{moduleName}", moduleName));
        sb.AppendLine(settings.BugScanFocus);
        sb.AppendLine();
        sb.AppendLine($"Project Number: {project.CrNumber}");
        sb.AppendLine($"Project Title: {TrimTo(project.Title, 300)}");
        sb.AppendLine($"Project Stage: {project.Stage}");
        sb.AppendLine($"Project Status: {project.Status}");
        if (!string.IsNullOrWhiteSpace(description))
            sb.AppendLine($"Description: {description}");
        if (!string.IsNullOrWhiteSpace(tech))
            sb.AppendLine($"Technology Stack: {tech}");
        sb.AppendLine($"Target Module: {moduleName}");

        sb.AppendLine();
        sb.AppendLine("Rules:");
        foreach (var rule in settings.ModuleScanRules)
            sb.AppendLine($"- {rule.Replace("{moduleName}", moduleName)}");
        if (!string.IsNullOrWhiteSpace(additionalInstructions))
        {
            sb.AppendLine("- Follow additional project-specific instructions below.");
            sb.AppendLine("Additional instructions:");
            sb.AppendLine(additionalInstructions);
        }

        var raw = TrimTo(sb.ToString(), 5000);
        var singleLine = Regex.Replace(raw, @"\s+", " ").Trim();
        return singleLine;
    }

    private static string BuildTestCaseGenPrompt(
        ChangeRequest project, int maxTestCases, string additionalInstructions, OpenClawSettings settings)
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
        sb.AppendLine(settings.TestCaseGenSystemRole);
        sb.AppendLine(settings.TestCaseGenOutputFormat);
        sb.AppendLine("Schema:");
        sb.AppendLine(settings.TestCaseGenSchema);
        sb.AppendLine();
        sb.AppendLine(settings.TestCaseGenInstruction.Replace("{maxTestCases}", maxTestCases.ToString()));
        sb.AppendLine(settings.TestCaseGenFocus);
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
            sb.AppendLine($"Repository Features: {string.Join(", ", repositoryFeatures)}");

        sb.AppendLine();
        sb.AppendLine("Rules:");
        foreach (var rule in settings.TestCaseGenRules)
            sb.AppendLine($"- {rule}");
        if (!string.IsNullOrWhiteSpace(additionalInstructions))
        {
            sb.AppendLine("- Follow additional project-specific instructions below.");
            sb.AppendLine("Additional instructions:");
            sb.AppendLine(additionalInstructions);
        }

        var raw = TrimTo(sb.ToString(), 5000);
        var singleLine = Regex.Replace(raw, @"\s+", " ").Trim();
        return singleLine;
    }

    private static string BuildFixPrompt(BugReport bug, string additionalInstructions, OpenClawSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine(settings.BugFixSystemRole);
        sb.AppendLine(settings.BugFixOutputFormat);
        sb.AppendLine("Keep response structured with sections:");
        foreach (var section in settings.BugFixSections)
            sb.AppendLine(section);
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
        foreach (var constraint in settings.BugFixConstraints)
            sb.AppendLine($"- {constraint}");
        if (!string.IsNullOrWhiteSpace(additionalInstructions))
        {
            sb.AppendLine("- Follow additional project-specific instructions below.");
            sb.AppendLine("Additional instructions:");
            sb.AppendLine(additionalInstructions);
        }

        var raw = TrimTo(sb.ToString(), 5000);
        return Regex.Replace(raw, @"\s+", " ").Trim();
    }

    private static IReadOnlyList<string> ParseScreenshotPaths(
        JsonElement findingElement,
        string? description,
        string? workflow,
        string? steps)
    {
        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;

            var normalized = raw.Trim().Trim('"', '\'', '`', '*', '.', ',', ';', ')', ']', '}');
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            if (seen.Add(normalized))
                results.Add(normalized);
        }

        if (findingElement.TryGetProperty("screenshotPaths", out var screenshotPathsElement))
        {
            if (screenshotPathsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in screenshotPathsElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        AddCandidate(item.GetString());
                }
            }
            else if (screenshotPathsElement.ValueKind == JsonValueKind.String)
            {
                var raw = screenshotPathsElement.GetString() ?? string.Empty;
                foreach (var token in raw.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                    AddCandidate(token);
            }
        }

        if (findingElement.TryGetProperty("screenshots", out var screenshotsElement))
        {
            if (screenshotsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in screenshotsElement.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        AddCandidate(item.GetString());
                }
            }
            else if (screenshotsElement.ValueKind == JsonValueKind.String)
            {
                AddCandidate(screenshotsElement.GetString());
            }
        }

        var pathPattern = @"(?:[A-Za-z]:\\|\\\\|/)[^\r\n]*?\.(?:png|jpg|jpeg|webp|gif|bmp)";
        var combined = $"{description}\n{workflow}\n{steps}";
        foreach (Match match in Regex.Matches(combined, pathPattern, RegexOptions.IgnoreCase))
            AddCandidate(match.Value);

        return results;
    }

    private static string BuildFeaturePrompt(ProjectFeature feature, ChangeRequest? project, string additionalInstructions, OpenClawSettings settings)
    {
        var sb = new StringBuilder();
        sb.AppendLine(settings.FeatureImplementSystemRole);
        sb.AppendLine(settings.FeatureImplementOutputFormat);
        foreach (var section in settings.FeatureImplementSections)
            sb.AppendLine(section);
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
        foreach (var constraint in settings.FeatureImplementConstraints)
            sb.AppendLine($"- {constraint}");
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

    private static string ReadFirstString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = ReadString(element, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static string NormalizeSeverityLabel(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var normalized = raw.ToLowerInvariant();
        var mapped = normalized switch
        {
            "critical" or "sev0" or "p0" or "blocker" or "showstopper" => "Critical",
            "high" or "sev1" or "p1" or "major" => "High",
            "medium" or "sev2" or "p2" or "normal" or "moderate" => "Medium",
            "low" or "sev3" or "p3" or "minor" or "trivial" or "cosmetic" => "Low",
            _ => string.Empty
        };

        if (!string.IsNullOrWhiteSpace(mapped))
            return mapped;

        if (normalized.Contains("critical") || normalized.Contains("blocker"))
            return "Critical";
        if (normalized.Contains("high"))
            return "High";
        if (normalized.Contains("low"))
            return "Low";
        if (normalized.Contains("medium"))
            return "Medium";

        return string.Empty;
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
        new(false, error, [], string.Empty);

    private static OpenClawBugFixResult FailedFix(string error) =>
        new(false, error, string.Empty);

    private static OpenClawFeatureImplementResult FailedFeature(string error) =>
        new(false, error, string.Empty);

    private static OpenClawTestCaseGenResult FailedTestCaseGen(string error) =>
        new(false, error, [], string.Empty);
}
