using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public class CodexSettings
{
    public string CliPath { get; set; } = "codex";
    public string BugScanAgentId { get; set; } = string.Empty;
    public List<string> BugScanAgentIds { get; set; } = [];
    public string BugFixAgentId { get; set; } = string.Empty;
    public List<string> BugFixAgentIds { get; set; } = [];
    public string FeatureImplementAgentId { get; set; } = string.Empty;
    public List<string> FeatureImplementAgentIds { get; set; } = [];
    public string BugScanPromptAdditionalInstructions { get; set; } = string.Empty;
    public List<string> BugScanPromptAdditionalInstructionLines { get; set; } = [];
    public string SecurityScanPromptAdditionalInstructions { get; set; } = string.Empty;
    public List<string> SecurityScanPromptAdditionalInstructionLines { get; set; } = [];
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

    // Security scan â€” specific prompt for vulnerability-focused QA scans
    public string SecurityScanInstruction { get; set; } = "Generate up to {findingsLimit} high-confidence security issues.";
    public string SecurityScanFocus { get; set; } = "Focus on security flaws such as auth bypass, injection, insecure data exposure, broken access control, secrets leakage, and unsafe defaults.";
    public List<string> SecurityScanRules { get; set; } =
    [
        "Every finding must include all schema fields.",
        "Report only exploitable or high-confidence security findings.",
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

    // Project health analysis prompt.
    public string ProjectHealthSystemRole { get; set; } = "You are a senior engineering manager analyzing software project delivery health.";
    public string ProjectHealthOutputFormat { get; set; } = "Return only JSON, no markdown and no extra text.";
    public string ProjectHealthSchema { get; set; } = "{\"score\":0,\"label\":\"Good\",\"summary\":\"...\",\"complexity\":\"Medium\",\"factors\":[\"...\"]}";
    public string ProjectHealthInstruction { get; set; } = "Analyze project health using bugs, timeline, features, and complexity inputs. Return an overall score from 0 to 100.";
    public string ProjectHealthFocus { get; set; } = "Be practical and prioritize delivery risk, quality risk, and timeline risk.";
    public List<string> ProjectHealthRules { get; set; } =
    [
        "score must be an integer from 0 to 100.",
        "label must be one of: Excellent, Good, At Risk, Critical.",
        "complexity must be one of: Low, Medium, High.",
        "summary must be concise and action-oriented (max 280 chars).",
        "factors must contain 3 to 5 short bullet-style risks or drivers."
    ];

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

public sealed class CodexBugScanService : ICodexBugScanService
{
    private const int DefaultTimeoutSeconds = 120;
    private const int DefaultFindingsPerScan = 8;
    private const int HardMaxFindings = 20;

    private readonly CodexSettings _settings;
    private readonly ILogger<CodexBugScanService> _logger;
    private readonly ISystemSettingsService _systemSettingsService;

    public CodexBugScanService(
        IOptions<CodexSettings> settings,
        ILogger<CodexBugScanService> logger,
        ISystemSettingsService systemSettingsService)
    {
        _settings = settings.Value ?? new CodexSettings();
        _logger = logger;
        _systemSettingsService = systemSettingsService;
    }

    public int MaxFindingsPerScan => NormalizeFindingsLimit(_settings.MaxFindingsPerScan);

    public async Task<CodexBugScanResult> ScanProjectAsync(
        ChangeRequest project,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default,
        bool useSecurityPrompt = false)
    {
        ArgumentNullException.ThrowIfNull(project);

        var configuredCliPath = string.IsNullOrWhiteSpace(_settings.CliPath)
            ? "codex"
            : _settings.CliPath.Trim();
        var execution = await ResolveExecutionSettingsAsync(ResolveRequestedAgentId(scanAgentId, _settings));
        var timeoutSeconds = NormalizeTimeout(_settings.TimeoutSeconds);
        var findingsLimit = MaxFindingsPerScan;
        var additionalInstructions = useSecurityPrompt
            ? ResolveSecurityScanAdditionalInstructions(_settings)
            : ResolveAdditionalInstructions(_settings);
        var prompt = useSecurityPrompt
            ? BuildSecurityPrompt(project, findingsLimit, additionalInstructions, _settings)
            : BuildPrompt(project, findingsLimit, additionalInstructions, _settings);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set Codex:CliPath to your Codex executable path."
                : $"Set Codex:CliPath to '{recommendedPath}'.";
            return Failed($"Codex scan failed: Codex CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, execution.Model, prompt, reasoningEffort: execution.ReasoningEffort);

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
                return Failed("Codex scan failed: unable to start Codex process.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start Codex process from path {CliPath}", resolvedCliPath);
            return Failed($"Codex scan failed: cannot start '{resolvedCliPath}'.");
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
            return Failed($"Codex scan timed out after {timeoutSeconds} seconds.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "Codex process exited with code {ExitCode}. stderr: {StdErr}",
                process.ExitCode,
                TrimTo(stderr, 400));

            var errorDetails = !string.IsNullOrWhiteSpace(stderr)
                ? TrimTo(stderr, 220)
                : "Codex command returned a non-zero exit code.";
            return Failed($"Codex scan failed: {errorDetails}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return Failed("Codex scan failed: command returned empty output.");

        if (!TryParseCodexResponse(stdout, out var agentText, out var responseError))
        {
            var directFindings = ParseFindingsFromRawCandidates(stdout, findingsLimit);
            if (directFindings.Count == 0 && !string.IsNullOrWhiteSpace(stderr))
                directFindings = ParseFindingsFromRawCandidates(stderr, findingsLimit);
            if (directFindings.Count == 0 && !string.IsNullOrWhiteSpace(stderr))
                directFindings = ParseFindingsFromRawCandidates($"{stdout}\n{stderr}", findingsLimit);

            if (directFindings.Count > 0)
            {
                _logger.LogInformation(
                    "Codex response envelope parse failed but direct finding extraction succeeded ({Count} findings).",
                    directFindings.Count);
                return new CodexBugScanResult(true, string.Empty, directFindings, stdout);
            }

            _logger.LogWarning(
                "Unable to parse Codex response. stdout: {StdOut}; stderr: {StdErr}",
                TrimTo(stdout, 400),
                TrimTo(stderr, 400));
            var preview = TrimTo(StripAnsi(stdout), 180);
            var previewMessage = string.IsNullOrWhiteSpace(preview) ? string.Empty : $" Output preview: {preview}";
            return Failed($"Codex scan failed: {responseError}.{previewMessage}");
        }

        if (string.IsNullOrWhiteSpace(agentText))
            return new CodexBugScanResult(true, string.Empty, [], stdout);

        var findings = ParseFindings(agentText, findingsLimit);
        if (findings.Count == 0)
        {
            _logger.LogInformation(
                "Codex scan returned no parseable findings for project {CrNumber}.",
                project.CrNumber);
            return new CodexBugScanResult(true, string.Empty, [], stdout);
        }

        return new CodexBugScanResult(true, string.Empty, findings, stdout);
    }

    public async Task<CodexBugScanResult> ScanModuleAsync(
        ChangeRequest project,
        string moduleName,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default,
        bool useSecurityPrompt = false)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);

        var configuredCliPath = string.IsNullOrWhiteSpace(_settings.CliPath)
            ? "codex"
            : _settings.CliPath.Trim();
        var execution = await ResolveExecutionSettingsAsync(ResolveRequestedAgentId(scanAgentId, _settings));
        var timeoutSeconds = NormalizeTimeout(_settings.TimeoutSeconds);
        var findingsLimit = MaxFindingsPerScan;
        var additionalInstructions = useSecurityPrompt
            ? ResolveSecurityScanAdditionalInstructions(_settings)
            : ResolveAdditionalInstructions(_settings);
        var prompt = useSecurityPrompt
            ? BuildSecurityModuleScanPrompt(project, moduleName.Trim(), findingsLimit, additionalInstructions, _settings)
            : BuildModuleScanPrompt(project, moduleName.Trim(), findingsLimit, additionalInstructions, _settings);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set Codex:CliPath to your Codex executable path."
                : $"Set Codex:CliPath to '{recommendedPath}'.";
            return Failed($"Codex scan failed: Codex CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, execution.Model, prompt, reasoningEffort: execution.ReasoningEffort);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
                return Failed("Codex scan failed: unable to start Codex process.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start Codex module scan process from path {CliPath}", resolvedCliPath);
            return Failed($"Codex scan failed: cannot start '{resolvedCliPath}'.");
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
            return Failed($"Codex module scan timed out after {timeoutSeconds} seconds.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "Codex module scan exited with code {ExitCode}. stderr: {StdErr}",
                process.ExitCode, TrimTo(stderr, 400));
            var errorDetails = !string.IsNullOrWhiteSpace(stderr)
                ? TrimTo(stderr, 220)
                : "Codex command returned a non-zero exit code.";
            return Failed($"Codex scan failed: {errorDetails}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return Failed("Codex scan failed: command returned empty output.");

        if (!TryParseCodexResponse(stdout, out var agentText, out var responseError))
        {
            var directFindings = ParseFindingsFromRawCandidates(stdout, findingsLimit);
            if (directFindings.Count == 0 && !string.IsNullOrWhiteSpace(stderr))
                directFindings = ParseFindingsFromRawCandidates(stderr, findingsLimit);
            if (directFindings.Count == 0 && !string.IsNullOrWhiteSpace(stderr))
                directFindings = ParseFindingsFromRawCandidates($"{stdout}\n{stderr}", findingsLimit);

            if (directFindings.Count > 0)
            {
                _logger.LogInformation(
                    "Codex module scan response parse failed but direct extraction succeeded ({Count} findings).",
                    directFindings.Count);
                return new CodexBugScanResult(true, string.Empty, directFindings, stdout);
            }

            var preview = TrimTo(StripAnsi(stdout), 180);
            var previewMessage = string.IsNullOrWhiteSpace(preview) ? string.Empty : $" Output preview: {preview}";
            return Failed($"Codex scan failed: {responseError}.{previewMessage}");
        }

        if (string.IsNullOrWhiteSpace(agentText))
            return new CodexBugScanResult(true, string.Empty, [], stdout);

        var findings = ParseFindings(agentText, findingsLimit);
        if (findings.Count == 0)
        {
            _logger.LogInformation(
                "Codex module scan returned no parseable findings for project {CrNumber} module {Module}.",
                project.CrNumber, moduleName);
            return new CodexBugScanResult(true, string.Empty, [], stdout);
        }

        return new CodexBugScanResult(true, string.Empty, findings, stdout);
    }

    public async Task<CodexTestCaseGenResult> GenerateTestCasesAsync(
        ChangeRequest project,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var configuredCliPath = string.IsNullOrWhiteSpace(_settings.CliPath)
            ? "codex"
            : _settings.CliPath.Trim();
        var execution = await ResolveExecutionSettingsAsync(ResolveRequestedAgentId(scanAgentId, _settings));
        var timeoutSeconds = NormalizeTimeout(_settings.TimeoutSeconds);
        var maxTestCases = Math.Clamp(_settings.MaxTestCasesPerGeneration, 1, 30);
        var additionalInstructions = ResolveTestCaseGenAdditionalInstructions(_settings);
        var prompt = BuildTestCaseGenPrompt(project, maxTestCases, additionalInstructions, _settings);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set Codex:CliPath to your Codex executable path."
                : $"Set Codex:CliPath to '{recommendedPath}'.";
            return FailedTestCaseGen($"Codex failed: CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, execution.Model, prompt, reasoningEffort: execution.ReasoningEffort);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
                return FailedTestCaseGen("Codex failed: unable to start process.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start Codex test case gen process from path {CliPath}", resolvedCliPath);
            return FailedTestCaseGen($"Codex failed: cannot start '{resolvedCliPath}'.");
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
            return FailedTestCaseGen($"Codex test case generation timed out after {timeoutSeconds} seconds.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("Codex test case gen exited with code {ExitCode}. stderr: {StdErr}",
                process.ExitCode, TrimTo(stderr, 400));
            var errorDetails = !string.IsNullOrWhiteSpace(stderr) ? TrimTo(stderr, 220) : "Non-zero exit code.";
            return FailedTestCaseGen($"Codex failed: {errorDetails}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return FailedTestCaseGen("Codex failed: empty output.");

        // Try envelope parse first, then raw
        string? agentText = null;
        if (TryParseCodexResponse(stdout, out var envelopeText, out _))
            agentText = envelopeText;
        else
            agentText = stdout;

        if (string.IsNullOrWhiteSpace(agentText))
            return new CodexTestCaseGenResult(true, string.Empty, [], stdout);

        var testCases = ParseGeneratedTestCases(agentText, maxTestCases);
        if (testCases.Count == 0)
        {
            _logger.LogInformation("Codex test case gen returned no parseable test cases for project {CrNumber}.", project.CrNumber);
            return new CodexTestCaseGenResult(true, string.Empty, [], stdout);
        }

        return new CodexTestCaseGenResult(true, string.Empty, testCases, stdout);
    }

    public async Task<CodexProjectHealthResult> AnalyzeProjectHealthAsync(
        ChangeRequest project,
        int totalBugs,
        int openBugs,
        int featureCount,
        int repositoryFeatureCount,
        int timelineDays,
        int complexityScore,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        var configuredCliPath = string.IsNullOrWhiteSpace(_settings.CliPath)
            ? "codex"
            : _settings.CliPath.Trim();
        var execution = await ResolveExecutionSettingsAsync(ResolveRequestedAgentId(scanAgentId, _settings));
        var timeoutSeconds = Math.Clamp(NormalizeTimeout(_settings.TimeoutSeconds), 20, 90);
        var prompt = BuildProjectHealthPrompt(
            project,
            totalBugs,
            openBugs,
            featureCount,
            repositoryFeatureCount,
            timelineDays,
            complexityScore,
            _settings);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set Codex:CliPath to your Codex executable path."
                : $"Set Codex:CliPath to '{recommendedPath}'.";
            return FailedProjectHealth($"Codex failed: CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, execution.Model, prompt, reasoningEffort: execution.ReasoningEffort);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
                return FailedProjectHealth("Codex failed: unable to start process.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start Codex project health process from path {CliPath}", resolvedCliPath);
            return FailedProjectHealth($"Codex failed: cannot start '{resolvedCliPath}'.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds + 15));
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return FailedProjectHealth($"Codex project health analysis timed out after {timeoutSeconds} seconds.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("Codex project health exited with code {ExitCode}. stderr: {StdErr}",
                process.ExitCode, TrimTo(stderr, 400));
            var errorDetails = !string.IsNullOrWhiteSpace(stderr) ? TrimTo(stderr, 220) : "Non-zero exit code.";
            return FailedProjectHealth($"Codex failed: {errorDetails}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return FailedProjectHealth("Codex failed: empty output.");

        string? agentText = null;
        if (TryParseCodexResponse(stdout, out var envelopeText, out _))
            agentText = envelopeText;
        else
            agentText = stdout;

        if (string.IsNullOrWhiteSpace(agentText))
            return FailedProjectHealth("Codex failed: empty health response.");

        var parsed = ParseProjectHealth(agentText, complexityScore);
        if (!parsed.Succeeded)
            return parsed with { AgentResponseText = stdout };

        return parsed with { AgentResponseText = stdout };
    }

    public async Task<CodexBugFixResult> FixBugAsync(
        BugReport bug,
        string? fixAgentId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bug);

        var configuredCliPath = string.IsNullOrWhiteSpace(_settings.CliPath)
            ? "codex"
            : _settings.CliPath.Trim();
        var execution = await ResolveExecutionSettingsAsync(ResolveRequestedFixAgentId(fixAgentId, _settings));
        var timeoutSeconds = NormalizeTimeout(_settings.TimeoutSeconds);
        var additionalInstructions = ResolveFixAdditionalInstructions(_settings);
        var prompt = BuildFixPrompt(bug, additionalInstructions, _settings);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set Codex:CliPath to your Codex executable path."
                : $"Set Codex:CliPath to '{recommendedPath}'.";
            return FailedFix($"Codex fix failed: Codex CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, execution.Model, prompt, reasoningEffort: execution.ReasoningEffort);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
                return FailedFix("Codex fix failed: unable to start Codex process.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start Codex fix process from path {CliPath}", resolvedCliPath);
            return FailedFix($"Codex fix failed: cannot start '{resolvedCliPath}'.");
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
            return FailedFix($"Codex fix timed out after {timeoutSeconds} seconds.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            var errorDetails = !string.IsNullOrWhiteSpace(stderr)
                ? TrimTo(stderr, 220)
                : "Codex command returned a non-zero exit code.";
            return FailedFix($"Codex fix failed: {errorDetails}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return FailedFix("Codex fix failed: command returned empty output.");

        string fixPlan;
        if (TryParseCodexResponse(stdout, out var agentText, out _))
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
            return FailedFix("Codex fix failed: empty fix response.");

        var pullRequestUrl = ExtractPullRequestUrl(fixPlan)
            ?? ExtractPullRequestUrl(stdout)
            ?? ExtractPullRequestUrl(stderr);
        var normalizedFixPlan = TrimTo(fixPlan, 3500);
        return new CodexBugFixResult(true, string.Empty, normalizedFixPlan, pullRequestUrl);
    }

    public async Task<CodexFeatureImplementResult> ImplementFeatureAsync(
        ProjectFeature feature,
        ChangeRequest? project = null,
        string? featureAgentId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feature);

        var configuredCliPath = string.IsNullOrWhiteSpace(_settings.CliPath)
            ? "codex"
            : _settings.CliPath.Trim();
        var execution = await ResolveExecutionSettingsAsync(ResolveRequestedFeatureAgentId(featureAgentId, _settings));
        var timeoutSeconds = NormalizeTimeout(_settings.TimeoutSeconds);
        var additionalInstructions = ResolveFeatureAdditionalInstructions(_settings);
        var prompt = BuildFeaturePrompt(feature, project, additionalInstructions, _settings);

        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set Codex:CliPath to your Codex executable path."
                : $"Set Codex:CliPath to '{recommendedPath}'.";
            return FailedFeature($"Codex feature run failed: Codex CLI not found. {hint}");
        }

        var startInfo = BuildProcessStartInfo(resolvedCliPath, execution.Model, prompt, reasoningEffort: execution.ReasoningEffort);
        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
                return FailedFeature("Codex feature run failed: unable to start Codex process.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to start Codex feature process from path {CliPath}", resolvedCliPath);
            return FailedFeature($"Codex feature run failed: cannot start '{resolvedCliPath}'.");
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
            return FailedFeature($"Codex feature run timed out after {timeoutSeconds} seconds.");
        }

        var stdout = (await stdoutTask).Trim();
        var stderr = (await stderrTask).Trim();

        if (process.ExitCode != 0)
        {
            var errorDetails = !string.IsNullOrWhiteSpace(stderr)
                ? TrimTo(stderr, 220)
                : "Codex command returned a non-zero exit code.";
            return FailedFeature($"Codex feature run failed: {errorDetails}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return FailedFeature("Codex feature run failed: command returned empty output.");

        string implementationPlan;
        if (TryParseCodexResponse(stdout, out var agentText, out _))
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
            return FailedFeature("Codex feature run failed: empty response.");

        return new CodexFeatureImplementResult(true, string.Empty, TrimTo(implementationPlan, 3500));
    }

    public async Task<CodexCodeReadinessResult> ScanCodeReadinessAsync(
        ChangeRequest project,
        string repositoryPath,
        string commitSha,
        string? scanAgentId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(repositoryPath) || !Directory.Exists(repositoryPath))
            return FailedCodeReadiness("Repository snapshot is unavailable.");

        var configuredCliPath = string.IsNullOrWhiteSpace(_settings.CliPath)
            ? "codex"
            : _settings.CliPath.Trim();
        var resolvedCliPath = ResolveCliExecutable(configuredCliPath);
        if (string.IsNullOrWhiteSpace(resolvedCliPath))
        {
            var recommendedPath = GetRecommendedWindowsCliPath();
            var hint = string.IsNullOrWhiteSpace(recommendedPath)
                ? "Set Codex:CliPath to your Codex executable path."
                : $"Set Codex:CliPath to '{recommendedPath}'.";
            return FailedCodeReadiness($"Codex readiness scan failed: Codex CLI not found. {hint}");
        }

        var schemaPath = Path.Combine(
            repositoryPath,
            $".softracker-readiness-schema-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(schemaPath, BuildCodeReadinessSchema(), cancellationToken);
            var prompt = BuildCodeReadinessPrompt(project, commitSha);
            var execution = await ResolveExecutionSettingsAsync(ResolveRequestedAgentId(scanAgentId, _settings));
            var startInfo = BuildProcessStartInfo(
                resolvedCliPath,
                execution.Model,
                prompt,
                repositoryPath,
                "read-only",
                schemaPath,
                stripSensitiveEnvironment: true,
                reasoningEffort: execution.ReasoningEffort);

            using var process = new Process { StartInfo = startInfo };
            try
            {
                if (!process.Start())
                    return FailedCodeReadiness("Codex readiness scan failed: unable to start Codex process.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start Codex readiness scan in {RepositoryPath}", repositoryPath);
                return FailedCodeReadiness($"Codex readiness scan failed: cannot start '{resolvedCliPath}'.");
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(300, NormalizeTimeout(_settings.TimeoutSeconds))));
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return FailedCodeReadiness("Codex readiness scan timed out before analysis completed.");
            }

            var stdout = StripAnsi((await stdoutTask).Trim());
            var stderr = StripAnsi((await stderrTask).Trim());
            if (process.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(stderr)
                    ? "Codex command returned a non-zero exit code."
                    : TrimTo(stderr, 300);
                return FailedCodeReadiness($"Codex readiness scan failed: {detail}");
            }

            var json = StripCodeFence(stdout).Trim();
            CodexCodeReadinessResult? result;
            try
            {
                result = JsonSerializer.Deserialize<CodexCodeReadinessResult>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Codex readiness output was not valid JSON: {Preview}", TrimTo(stdout, 500));
                return FailedCodeReadiness("Codex readiness scan returned an invalid structured response.");
            }

            if (result is null)
                return FailedCodeReadiness("Codex readiness scan returned an empty response.");

            result.Succeeded = true;
            result.Error = string.Empty;
            result.Score = Math.Clamp(result.Score, 0, 100);
            result.Summary = TrimTo(result.Summary, 1500);
            result.AnalyzedFiles = Math.Max(0, result.AnalyzedFiles);
            result.Categories = result.Categories
                .Where(category => !string.IsNullOrWhiteSpace(category.Name))
                .Take(10)
                .Select(category => new CodexCodeReadinessCategory
                {
                    Name = TrimTo(category.Name, 80),
                    Score = Math.Clamp(category.Score, 0, 100),
                    Summary = TrimTo(category.Summary, 500)
                })
                .ToList();
            result.Findings = result.Findings
                .Where(finding => !string.IsNullOrWhiteSpace(finding.Title))
                .Take(50)
                .Select(NormalizeCodeReadinessFinding)
                .ToList();
            result.Solid = result.Solid
                .Where(check => !string.IsNullOrWhiteSpace(check.Principle))
                .Take(5)
                .Select(check => new CodexSolidReview
                {
                    Principle = TrimTo(check.Principle.ToUpperInvariant(), 5),
                    Status = NormalizeSolidStatus(check.Status),
                    Summary = TrimTo(check.Summary, 500)
                })
                .ToList();
            result.DesignPatterns = (result.DesignPatterns ?? [])
                .Where(pattern => !string.IsNullOrWhiteSpace(pattern.Name))
                .Take(15)
                .Select(NormalizeDesignPattern)
                .ToList();
            result.Owasp = NormalizeOwaspAssessments(result.Owasp);

            return result;
        }
        finally
        {
            try
            {
                if (File.Exists(schemaPath))
                    File.Delete(schemaPath);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to remove temporary Codex output schema {SchemaPath}", schemaPath);
            }
        }
    }

    private static int NormalizeTimeout(int configuredTimeoutSeconds)
    {
        if (configuredTimeoutSeconds <= 0)
            return DefaultTimeoutSeconds;

        return Math.Clamp(configuredTimeoutSeconds, 10, 900);
    }

    private static string ResolveAdditionalInstructions(CodexSettings settings)
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

    private static string ResolveSecurityScanAdditionalInstructions(CodexSettings settings)
    {
        var instructions = new List<string>();

        if (!string.IsNullOrWhiteSpace(settings.SecurityScanPromptAdditionalInstructions))
            instructions.Add(settings.SecurityScanPromptAdditionalInstructions.Trim());

        if (settings.SecurityScanPromptAdditionalInstructionLines is not null)
        {
            instructions.AddRange(settings.SecurityScanPromptAdditionalInstructionLines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Trim()));
        }

        return string.Join("\n", instructions);
    }

    private static string ResolveFixAdditionalInstructions(CodexSettings settings)
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

    private static string ResolveFeatureAdditionalInstructions(CodexSettings settings)
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

    private static string ResolveTestCaseGenAdditionalInstructions(CodexSettings settings)
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

    private async Task<CodexExecutionSettings> ResolveExecutionSettingsAsync(string? requestedModel)
    {
        var preferences = await _systemSettingsService.GetCodexAiSettingsAsync();
        var useSavedModel = string.IsNullOrWhiteSpace(requestedModel) ||
                            requestedModel.Equals("main", StringComparison.OrdinalIgnoreCase) ||
                            requestedModel.Equals("default", StringComparison.OrdinalIgnoreCase);
        var model = useSavedModel ? preferences.Model : requestedModel!.Trim();
        var reasoning = CodexAiDefaults.ReasoningEfforts.FirstOrDefault(value =>
                            value.Equals(preferences.ReasoningEffort, StringComparison.OrdinalIgnoreCase))
                        ?? CodexAiDefaults.ReasoningEffort;
        return new CodexExecutionSettings(model, reasoning);
    }

    private static string ResolveRequestedAgentId(string? scanAgentId, CodexSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(scanAgentId))
            return scanAgentId.Trim();

        var configured = GetConfiguredAgentIds(settings);
        if (configured.Count > 0)
            return configured[0];

        return string.Empty;
    }

    private static string ResolveRequestedFixAgentId(string? fixAgentId, CodexSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(fixAgentId))
            return fixAgentId.Trim();

        var configured = GetConfiguredFixAgentIds(settings);
        if (configured.Count > 0)
            return configured[0];

        return string.Empty;
    }

    private static string ResolveRequestedFeatureAgentId(string? featureAgentId, CodexSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(featureAgentId))
            return featureAgentId.Trim();

        var configured = GetConfiguredFeatureAgentIds(settings);
        if (configured.Count > 0)
            return configured[0];

        return string.Empty;
    }

    public static IReadOnlyList<string> GetConfiguredAgentIds(CodexSettings settings)
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

    public static IReadOnlyList<string> GetConfiguredFixAgentIds(CodexSettings settings)
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

        return result;
    }

    public static IReadOnlyList<string> GetConfiguredFeatureAgentIds(CodexSettings settings)
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

        return result;
    }

    private static ProcessStartInfo BuildProcessStartInfo(
        string resolvedCliPath,
        string model,
        string prompt,
        string? workingDirectory = null,
        string sandbox = "workspace-write",
        string? outputSchemaPath = null,
        bool stripSensitiveEnvironment = false,
        string reasoningEffort = CodexAiDefaults.ReasoningEffort)
    {
        var args = new List<string>
        {
            "exec",
            "--ephemeral",
            "--ignore-user-config",
            "--color",
            "never",
            "--sandbox",
            sandbox,
            "--skip-git-repo-check"
        };

        if (!string.IsNullOrWhiteSpace(outputSchemaPath))
        {
            args.Add("--output-schema");
            args.Add(outputSchemaPath);
        }

        if (!string.IsNullOrWhiteSpace(model) &&
            !model.Equals("main", StringComparison.OrdinalIgnoreCase) &&
            !model.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--model");
            args.Add(model.Trim());
        }

        args.Add("--config");
        args.Add($"model_reasoning_effort=\"{reasoningEffort}\"");

        args.Add(prompt);

        var usePowerShellHost = resolvedCliPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
        var useCommandHost = resolvedCliPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
            resolvedCliPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo
        {
            FileName = usePowerShellHost
                ? "powershell"
                : useCommandHost ? "cmd.exe" : resolvedCliPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
            startInfo.WorkingDirectory = Path.GetFullPath(workingDirectory);

        if (stripSensitiveEnvironment)
        {
            var sensitiveNames = startInfo.Environment.Keys
                .Where(IsSensitiveEnvironmentVariable)
                .ToList();
            foreach (var name in sensitiveNames)
                startInfo.Environment.Remove(name);
        }

        if (usePowerShellHost)
        {
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(resolvedCliPath);
        }
        else if (useCommandHost)
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/s");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(resolvedCliPath);
        }

        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        return startInfo;
    }

    private sealed record CodexExecutionSettings(string Model, string ReasoningEffort);

    private static bool IsSensitiveEnvironmentVariable(string name)
    {
        var normalized = name.Replace("_", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        return normalized.Contains("SECRET", StringComparison.Ordinal) ||
               normalized.Contains("TOKEN", StringComparison.Ordinal) ||
               normalized.Contains("PASSWORD", StringComparison.Ordinal) ||
               normalized.Contains("APIKEY", StringComparison.Ordinal) ||
               normalized.Contains("CONNECTIONSTRING", StringComparison.Ordinal) ||
               normalized.StartsWith("GITHUB", StringComparison.Ordinal) ||
               normalized.StartsWith("STRIPE", StringComparison.Ordinal) ||
               normalized.StartsWith("JWT", StringComparison.Ordinal);
    }

    private static string? ResolveCliExecutable(string configuredCliPath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var defaultCommand = string.IsNullOrWhiteSpace(configuredCliPath)
            ? "codex"
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
        var envCliPath = Environment.GetEnvironmentVariable("CODEX_CLI_PATH")?.Trim();
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
            yield return Path.Combine(appData, "npm", "codex.cmd");
            yield return Path.Combine(appData, "npm", "codex.ps1");
            yield return Path.Combine(appData, "npm", "codex.exe");
        }

        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            yield return Path.Combine(userProfile, "AppData", "Roaming", "npm", "codex.cmd");
            yield return Path.Combine(userProfile, "AppData", "Roaming", "npm", "codex.ps1");

            var extensionsDirectory = Path.Combine(userProfile, ".vscode", "extensions");
            if (Directory.Exists(extensionsDirectory))
            {
                foreach (var executable in Directory
                    .EnumerateFiles(extensionsDirectory, "codex.exe", SearchOption.AllDirectories)
                    .Where(path => path.Contains("openai.chatgpt-", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    yield return executable;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(localAppData))
            yield return Path.Combine(localAppData, "pnpm", "codex.cmd");
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

        var path = Path.Combine(appData, "npm", "codex.cmd");
        return File.Exists(path) ? path : string.Empty;
    }

    private static int NormalizeFindingsLimit(int configuredLimit)
    {
        if (configuredLimit <= 0)
            return DefaultFindingsPerScan;

        return Math.Clamp(configuredLimit, 1, HardMaxFindings);
    }

    private static string NormalizeHealthLabel(string? raw, int score)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (raw.Equals("Excellent", StringComparison.OrdinalIgnoreCase)) return "Excellent";
            if (raw.Equals("Good", StringComparison.OrdinalIgnoreCase)) return "Good";
            if (raw.Equals("At Risk", StringComparison.OrdinalIgnoreCase) || raw.Equals("AtRisk", StringComparison.OrdinalIgnoreCase)) return "At Risk";
            if (raw.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return "Critical";
        }

        return score switch
        {
            >= 85 => "Excellent",
            >= 70 => "Good",
            >= 45 => "At Risk",
            _ => "Critical"
        };
    }

    private static string NormalizeComplexity(string? raw, int complexityScore)
    {
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (raw.Equals("Low", StringComparison.OrdinalIgnoreCase)) return "Low";
            if (raw.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return "Medium";
            if (raw.Equals("High", StringComparison.OrdinalIgnoreCase)) return "High";
        }

        return complexityScore switch
        {
            >= 70 => "High",
            >= 40 => "Medium",
            _ => "Low"
        };
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

    private static bool TryParseCodexResponse(
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

    private static List<CodexBugFinding> ParseFindings(string agentText, int maxFindings)
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

            var results = new List<CodexBugFinding>();
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
                    description = "Generated by Codex scan.";
                if (string.IsNullOrWhiteSpace(workflow))
                    workflow = "Review project flow and run targeted validation for this issue.";
                if (string.IsNullOrWhiteSpace(steps))
                    steps = "1. Open the impacted flow.\n2. Execute the scenario.\n3. Validate expected behavior.";
                if (string.IsNullOrWhiteSpace(module))
                    module = "General";
                if (string.IsNullOrWhiteSpace(severity))
                    severity = "Medium";

                results.Add(new CodexBugFinding(
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

    private static List<CodexGeneratedTestCase> ParseGeneratedTestCases(string agentText, int maxItems)
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

            var results = new List<CodexGeneratedTestCase>();
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

                results.Add(new CodexGeneratedTestCase(name, description, module, category, environment));

                if (results.Count >= maxItems)
                    break;
            }

            return results;
        }
    }

    private static CodexProjectHealthResult ParseProjectHealth(string agentText, int complexityScore)
    {
        var cleaned = StripCodeFence(agentText);
        if (!TryParseJsonDocument(cleaned, out var json))
            return FailedProjectHealth("Codex returned non-JSON health output.");

        using (json)
        {
            var root = json.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("projectHealth", out var wrapped) &&
                wrapped.ValueKind == JsonValueKind.Object)
            {
                root = wrapped;
            }

            if (root.ValueKind != JsonValueKind.Object)
                return FailedProjectHealth("Codex health output schema is invalid.");

            var rawScore = root.TryGetProperty("score", out var scoreEl) && scoreEl.ValueKind == JsonValueKind.Number && scoreEl.TryGetInt32(out var s)
                ? s
                : 50;
            var score = Math.Clamp(rawScore, 0, 100);

            var label = NormalizeHealthLabel(TrimTo(ReadString(root, "label"), 40), score);
            var summary = TrimTo(ReadString(root, "summary"), 280);
            if (string.IsNullOrWhiteSpace(summary))
                summary = "Health analysis completed.";

            var complexity = NormalizeComplexity(
                TrimTo(ReadString(root, "complexity"), 30),
                complexityScore);

            var factors = new List<string>();
            if (root.TryGetProperty("factors", out var factorsEl) && factorsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in factorsEl.EnumerateArray())
                {
                    if (item.ValueKind is not JsonValueKind.String)
                        continue;

                    var factor = TrimTo(item.GetString(), 120);
                    if (string.IsNullOrWhiteSpace(factor) || factors.Contains(factor, StringComparer.OrdinalIgnoreCase))
                        continue;

                    factors.Add(factor);
                    if (factors.Count >= 5)
                        break;
                }
            }

            return new CodexProjectHealthResult(
                true,
                string.Empty,
                score,
                label,
                summary,
                complexity,
                factors,
                string.Empty);
        }
    }

    private static List<CodexBugFinding> ParseFindingsFromRawCandidates(string raw, int maxFindings)
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

    private static string BuildCodeReadinessPrompt(ChangeRequest project, string commitSha)
    {
        var prompt = $"""
            You are performing a read-only, source-level code readiness review of the repository in the current working directory.
            Project: {TrimTo(project.Title, 200)} ({TrimTo(project.CrNumber, 30)}). Commit: {TrimTo(commitSha, 64)}.
            Inspect the actual application source comprehensively using repository-wide searches and direct file reads. Do not judge from folder names alone.
            Review SOLID principles (SRP, OCP, LSP, ISP, DIP), architecture boundaries, maintainability, security, automated tests, error handling, configuration, persistence, and incomplete implementation.
            Identify design and architectural code patterns actually implemented, such as MVC, layered architecture, repository, unit of work, service layer, dependency injection, factory, strategy, adapter, observer, mediator, or CQRS. Include a pattern only when direct source evidence exists, mark it Implemented or Partial, and cite up to five repository-relative files. Do not list a framework feature as a pattern without implementation evidence.
            Assess every OWASP Top 10:2025 category exactly once: A01 Broken Access Control, A02 Security Misconfiguration, A03 Software Supply Chain Failures, A04 Cryptographic Failures, A05 Injection, A06 Insecure Design, A07 Authentication Failures, A08 Software or Data Integrity Failures, A09 Security Logging and Alerting Failures, and A10 Mishandling of Exceptional Conditions. Use Pass only when positive source evidence supports it, Fail only for a directly evidenced weakness, Warning for partial controls or material risk, NotApplicable only when clearly irrelevant, and Unknown when source-only review cannot establish the result. Cite a repository-relative file and line for evidence when available and provide a concrete recommendation for every Warning or Fail. Do not claim that runtime configuration, deployed infrastructure, or dependency vulnerability status was tested.
            Ignore .git, node_modules, bin, obj, dist, coverage, vendor, generated files, lock files, minified assets, and every .softracker-readiness-schema-* file.
            Do not edit files, install dependencies, build the application, run tests, execute repository scripts, or access the network.
            Every failing or warning finding must cite a repository-relative file and the best available one-based line number. Do not invent files, lines, vulnerabilities, or principles.
            Use Unknown when evidence is insufficient. Return no more than 50 findings, ordered by severity and impact.
            Return only JSON matching the supplied schema. Categories should include Structure, Tests, Security, Maintainability, and Completeness.
            """;

        return Regex.Replace(prompt, @"\s+", " ").Trim();
    }

    private static string BuildCodeReadinessSchema() => """
        {
          "type": "object",
          "properties": {
            "score": { "type": "integer", "minimum": 0, "maximum": 100 },
            "summary": { "type": "string" },
            "analyzedFiles": { "type": "integer", "minimum": 0 },
            "categories": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "name": { "type": "string" },
                  "score": { "type": "integer", "minimum": 0, "maximum": 100 },
                  "summary": { "type": "string" }
                },
                "required": ["name", "score", "summary"],
                "additionalProperties": false
              }
            },
            "findings": {
              "type": "array",
              "maxItems": 50,
              "items": {
                "type": "object",
                "properties": {
                  "principle": { "type": "string", "enum": ["SRP", "OCP", "LSP", "ISP", "DIP", "None"] },
                  "category": { "type": "string" },
                  "severity": { "type": "string", "enum": ["Critical", "High", "Medium", "Low"] },
                  "title": { "type": "string" },
                  "evidence": { "type": "string" },
                  "recommendation": { "type": "string" },
                  "file": { "type": "string" },
                  "line": { "type": ["integer", "null"], "minimum": 1 },
                  "confidence": { "type": "integer", "minimum": 0, "maximum": 100 }
                },
                "required": ["principle", "category", "severity", "title", "evidence", "recommendation", "file", "line", "confidence"],
                "additionalProperties": false
              }
            },
            "solid": {
              "type": "array",
              "minItems": 5,
              "maxItems": 5,
              "items": {
                "type": "object",
                "properties": {
                  "principle": { "type": "string", "enum": ["SRP", "OCP", "LSP", "ISP", "DIP"] },
                  "status": { "type": "string", "enum": ["Pass", "Warning", "Fail", "Unknown"] },
                  "summary": { "type": "string" }
                },
                "required": ["principle", "status", "summary"],
                "additionalProperties": false
              }
            },
            "designPatterns": {
              "type": "array",
              "maxItems": 15,
              "items": {
                "type": "object",
                "properties": {
                  "name": { "type": "string" },
                  "category": { "type": "string", "enum": ["Architectural", "Creational", "Structural", "Behavioral", "Enterprise", "Other"] },
                  "status": { "type": "string", "enum": ["Implemented", "Partial"] },
                  "summary": { "type": "string" },
                  "files": {
                    "type": "array",
                    "maxItems": 5,
                    "items": { "type": "string" }
                  },
                  "confidence": { "type": "integer", "minimum": 0, "maximum": 100 }
                },
                "required": ["name", "category", "status", "summary", "files", "confidence"],
                "additionalProperties": false
              }
            },
            "owasp": {
              "type": "array",
              "minItems": 10,
              "maxItems": 10,
              "items": {
                "type": "object",
                "properties": {
                  "id": { "type": "string", "enum": ["A01:2025", "A02:2025", "A03:2025", "A04:2025", "A05:2025", "A06:2025", "A07:2025", "A08:2025", "A09:2025", "A10:2025"] },
                  "status": { "type": "string", "enum": ["Pass", "Warning", "Fail", "NotApplicable", "Unknown"] },
                  "summary": { "type": "string" },
                  "evidence": { "type": "string" },
                  "recommendation": { "type": "string" },
                  "file": { "type": "string" },
                  "line": { "type": ["integer", "null"], "minimum": 1 },
                  "confidence": { "type": "integer", "minimum": 0, "maximum": 100 }
                },
                "required": ["id", "status", "summary", "evidence", "recommendation", "file", "line", "confidence"],
                "additionalProperties": false
              }
            }
          },
          "required": ["score", "summary", "analyzedFiles", "categories", "findings", "solid", "designPatterns", "owasp"],
          "additionalProperties": false
        }
        """;

    private static CodexCodeReadinessFinding NormalizeCodeReadinessFinding(CodexCodeReadinessFinding finding)
    {
        var severity = finding.Severity?.Trim();
        if (severity is not ("Critical" or "High" or "Medium" or "Low"))
            severity = "Low";

        var principle = finding.Principle?.Trim().ToUpperInvariant();
        if (principle is not ("SRP" or "OCP" or "LSP" or "ISP" or "DIP"))
            principle = "None";

        return new CodexCodeReadinessFinding
        {
            Principle = principle,
            Category = TrimTo(finding.Category, 80),
            Severity = severity,
            Title = TrimTo(finding.Title, 300),
            Evidence = TrimTo(finding.Evidence, 1500),
            Recommendation = TrimTo(finding.Recommendation, 1000),
            File = TrimTo(finding.File, 500).Replace('\\', '/').TrimStart('/'),
            Line = finding.Line is > 0 ? finding.Line : null,
            Confidence = Math.Clamp(finding.Confidence, 0, 100)
        };
    }

    private static string NormalizeSolidStatus(string? status) => status?.Trim() switch
    {
        "Pass" => "Pass",
        "Warning" => "Warning",
        "Fail" => "Fail",
        _ => "Unknown"
    };

    private static CodexDesignPattern NormalizeDesignPattern(CodexDesignPattern pattern)
    {
        var allowedCategories = new HashSet<string>(StringComparer.Ordinal)
        {
            "Architectural", "Creational", "Structural", "Behavioral", "Enterprise", "Other"
        };
        var category = pattern.Category?.Trim() ?? string.Empty;
        if (!allowedCategories.Contains(category))
            category = "Other";

        var status = pattern.Status?.Trim() == "Implemented" ? "Implemented" : "Partial";
        var files = (pattern.Files ?? [])
            .Where(file => !string.IsNullOrWhiteSpace(file))
            .Select(file => TrimTo(file, 500).Replace('\\', '/').TrimStart('/'))
            .Where(file => !file.Contains("..", StringComparison.Ordinal) && !Path.IsPathRooted(file))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        return new CodexDesignPattern
        {
            Name = TrimTo(pattern.Name, 120),
            Category = category,
            Status = status,
            Summary = TrimTo(pattern.Summary, 750),
            Files = files,
            Confidence = Math.Clamp(pattern.Confidence, 0, 100)
        };
    }

    private static List<CodexOwaspAssessment> NormalizeOwaspAssessments(
        IEnumerable<CodexOwaspAssessment>? assessments)
    {
        var categories = new (string Id, string Name)[]
        {
            ("A01:2025", "Broken Access Control"),
            ("A02:2025", "Security Misconfiguration"),
            ("A03:2025", "Software Supply Chain Failures"),
            ("A04:2025", "Cryptographic Failures"),
            ("A05:2025", "Injection"),
            ("A06:2025", "Insecure Design"),
            ("A07:2025", "Authentication Failures"),
            ("A08:2025", "Software or Data Integrity Failures"),
            ("A09:2025", "Security Logging and Alerting Failures"),
            ("A10:2025", "Mishandling of Exceptional Conditions")
        };
        var supplied = (assessments ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return categories.Select(category =>
        {
            supplied.TryGetValue(category.Id, out var assessment);
            assessment ??= new CodexOwaspAssessment
            {
                Id = category.Id,
                Status = "Unknown",
                Summary = "The source scan returned no conclusion for this category."
            };
            var status = assessment.Status?.Trim() switch
            {
                "Pass" => "Pass",
                "Warning" => "Warning",
                "Fail" => "Fail",
                "NotApplicable" or "Not Applicable" => "Not Applicable",
                _ => "Unknown"
            };
            var file = TrimTo(assessment.File, 500).Replace('\\', '/').TrimStart('/');
            if (file.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(file))
                file = string.Empty;

            return new CodexOwaspAssessment
            {
                Id = category.Id,
                Name = category.Name,
                Status = status,
                Summary = TrimTo(assessment.Summary, 750),
                Evidence = TrimTo(assessment.Evidence, 1000),
                Recommendation = TrimTo(assessment.Recommendation, 1000),
                File = file,
                Line = assessment.Line is > 0 ? assessment.Line : null,
                Confidence = Math.Clamp(assessment.Confidence, 0, 100)
            };
        }).ToList();
    }

    private static CodexCodeReadinessResult FailedCodeReadiness(string error) => new()
    {
        Succeeded = false,
        Error = TrimTo(error, 500)
    };

    private static string BuildProjectHealthPrompt(
        ChangeRequest project,
        int totalBugs,
        int openBugs,
        int featureCount,
        int repositoryFeatureCount,
        int timelineDays,
        int complexityScore,
        CodexSettings settings)
    {
        var features = project.Features
            .Select(f => f.Name?.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Take(12)
            .ToList();

        var repositoryFeatures = project.RepositoryFeatures
            .Select(f => f.Name?.Trim())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Take(12)
            .ToList();

        var description = TrimTo(project.Description, 1200);
        var tech = TrimTo(project.TechnologyStack, 400);

        var sb = new StringBuilder();
        sb.AppendLine(settings.ProjectHealthSystemRole);
        sb.AppendLine(settings.ProjectHealthOutputFormat);
        sb.AppendLine("Schema:");
        sb.AppendLine(settings.ProjectHealthSchema);
        sb.AppendLine();
        sb.AppendLine(settings.ProjectHealthInstruction);
        sb.AppendLine(settings.ProjectHealthFocus);
        sb.AppendLine();
        sb.AppendLine($"Project Number: {project.CrNumber}");
        sb.AppendLine($"Project Title: {TrimTo(project.Title, 300)}");
        sb.AppendLine($"Project Stage: {project.Stage}");
        sb.AppendLine($"Project Status: {project.Status}");
        if (!string.IsNullOrWhiteSpace(description))
            sb.AppendLine($"Description: {description}");
        if (!string.IsNullOrWhiteSpace(tech))
            sb.AppendLine($"Technology Stack: {tech}");
        if (!string.IsNullOrWhiteSpace(project.GitHubRepoUrl))
            sb.AppendLine($"Repository URL: {TrimTo(project.GitHubRepoUrl, 500)}");
        if (!string.IsNullOrWhiteSpace(project.GitHubBranch))
            sb.AppendLine($"Repository Branch: {TrimTo(project.GitHubBranch, 100)}");
        if (features.Count > 0)
            sb.AppendLine($"Project Features: {string.Join(", ", features)}");
        if (repositoryFeatures.Count > 0)
            sb.AppendLine($"Repository Features: {string.Join(", ", repositoryFeatures)}");
        sb.AppendLine($"Metrics - Total Bugs: {Math.Max(0, totalBugs)}");
        sb.AppendLine($"Metrics - Open Bugs: {Math.Max(0, openBugs)}");
        sb.AppendLine($"Metrics - Feature Count: {Math.Max(0, featureCount)}");
        sb.AppendLine($"Metrics - Repository Feature Count: {Math.Max(0, repositoryFeatureCount)}");
        sb.AppendLine($"Metrics - Timeline Days: {Math.Max(0, timelineDays)}");
        sb.AppendLine($"Metrics - Complexity Score (0-100): {Math.Clamp(complexityScore, 0, 100)}");

        sb.AppendLine();
        sb.AppendLine("Rules:");
        foreach (var rule in settings.ProjectHealthRules)
            sb.AppendLine($"- {rule}");

        var raw = TrimTo(sb.ToString(), 5000);
        return Regex.Replace(raw, @"\s+", " ").Trim();
    }

    private static string BuildPrompt(ChangeRequest project, int findingsLimit, string additionalInstructions, CodexSettings settings)
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
        // Command wrappers can lose multi-line argument content on Windows; send a one-line prompt.
        var singleLine = Regex.Replace(raw, @"\s+", " ").Trim();
        return singleLine;
    }

    private static string BuildSecurityPrompt(ChangeRequest project, int findingsLimit, string additionalInstructions, CodexSettings settings)
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
        var securityRules = settings.SecurityScanRules is { Count: > 0 } ? settings.SecurityScanRules : settings.BugScanRules;

        var sb = new StringBuilder();
        sb.AppendLine(settings.BugScanSystemRole);
        sb.AppendLine(settings.BugScanOutputFormat);
        sb.AppendLine("Schema:");
        sb.AppendLine(settings.BugScanSchema);
        sb.AppendLine();
        sb.AppendLine(settings.SecurityScanInstruction.Replace("{findingsLimit}", findingsLimit.ToString()));
        sb.AppendLine(settings.SecurityScanFocus);
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
        if (!string.IsNullOrWhiteSpace(project.GitHubRepoUrl))
            sb.AppendLine($"Repository URL: {TrimTo(project.GitHubRepoUrl, 500)}");
        if (!string.IsNullOrWhiteSpace(project.GitHubBranch))
            sb.AppendLine($"Repository Branch: {TrimTo(project.GitHubBranch, 100)}");
        if (features.Count > 0)
            sb.AppendLine($"Project Features: {string.Join(", ", features)}");
        if (repositoryFeatures.Count > 0)
            sb.AppendLine($"Repository Scan Features: {string.Join(", ", repositoryFeatures)}");

        sb.AppendLine();
        sb.AppendLine("Rules:");
        foreach (var rule in securityRules)
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

    private static string BuildSecurityModuleScanPrompt(
        ChangeRequest project, string moduleName, int findingsLimit, string additionalInstructions, CodexSettings settings)
    {
        var description = TrimTo(project.Description, 1200);
        var tech = TrimTo(project.TechnologyStack, 400);
        var securityRules = settings.SecurityScanRules is { Count: > 0 } ? settings.SecurityScanRules : settings.BugScanRules;

        var sb = new StringBuilder();
        sb.AppendLine(settings.BugScanSystemRole);
        sb.AppendLine(settings.BugScanOutputFormat);
        sb.AppendLine("Schema:");
        sb.AppendLine(settings.BugScanSchema);
        sb.AppendLine();
        sb.AppendLine(settings.SecurityScanInstruction
            .Replace("{findingsLimit}", findingsLimit.ToString())
            .Replace("{moduleName}", moduleName));
        sb.AppendLine(settings.SecurityScanFocus.Replace("{moduleName}", moduleName));
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
        foreach (var rule in securityRules)
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

    private static string BuildModuleScanPrompt(
        ChangeRequest project, string moduleName, int findingsLimit, string additionalInstructions, CodexSettings settings)
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
        ChangeRequest project, int maxTestCases, string additionalInstructions, CodexSettings settings)
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
        if (!string.IsNullOrWhiteSpace(project.GitHubRepoUrl))
            sb.AppendLine($"Repository URL: {TrimTo(project.GitHubRepoUrl, 500)}");
        if (!string.IsNullOrWhiteSpace(project.GitHubBranch))
            sb.AppendLine($"Repository Branch: {TrimTo(project.GitHubBranch, 100)}");
        if (features.Count > 0)
            sb.AppendLine($"Project Features: {string.Join(", ", features)}");
        if (repositoryFeatures.Count > 0)
            sb.AppendLine($"Repository Features: {string.Join(", ", repositoryFeatures)}");

        sb.AppendLine();
        sb.AppendLine("Generation Basis:");
        sb.AppendLine("- Use project modules, repository context, and major workflows.");
        sb.AppendLine("- Do NOT derive test cases from existing bugs or linked bug titles.");
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

    private static string BuildFixPrompt(BugReport bug, string additionalInstructions, CodexSettings settings)
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
        if (bug.ChangeRequest is not null)
        {
            sb.AppendLine($"Project Number: {bug.ChangeRequest.CrNumber}");
            sb.AppendLine($"Project Title: {TrimTo(bug.ChangeRequest.Title, 300)}");
            if (!string.IsNullOrWhiteSpace(bug.ChangeRequest.GitHubRepoUrl))
                sb.AppendLine($"Repository URL: {TrimTo(bug.ChangeRequest.GitHubRepoUrl, 500)}");
            if (!string.IsNullOrWhiteSpace(bug.ChangeRequest.GitHubBranch))
                sb.AppendLine($"Repository Branch: {TrimTo(bug.ChangeRequest.GitHubBranch, 100)}");
        }

        sb.AppendLine();
        sb.AppendLine("Constraints:");
        foreach (var constraint in settings.BugFixConstraints)
            sb.AppendLine($"- {constraint}");
        sb.AppendLine("- If you create a pull request, include the exact URL on its own line as: PR_URL: https://...");
        if (!string.IsNullOrWhiteSpace(additionalInstructions))
        {
            sb.AppendLine("- Follow additional project-specific instructions below.");
            sb.AppendLine("Additional instructions:");
            sb.AppendLine(additionalInstructions);
        }

        var raw = TrimTo(sb.ToString(), 5000);
        return Regex.Replace(raw, @"\s+", " ").Trim();
    }

    private static string? ExtractPullRequestUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var directLabelMatch = Regex.Match(
            text,
            @"\bPR_URL\s*:\s*(?<url>[^\s<>\]\)\}""'`]+)",
            RegexOptions.IgnoreCase);
        if (directLabelMatch.Success)
        {
            var labeledUrl = NormalizePossibleUrl(directLabelMatch.Groups["url"].Value);
            if (!string.IsNullOrWhiteSpace(labeledUrl) && IsPullRequestUrl(labeledUrl))
                return labeledUrl;
        }

        foreach (Match match in Regex.Matches(text, @"(?:https?://|www\.)[^\s<>\]\)\}""'`]+", RegexOptions.IgnoreCase))
        {
            var candidate = NormalizePossibleUrl(match.Value);
            if (!string.IsNullOrWhiteSpace(candidate) && IsPullRequestUrl(candidate))
                return candidate;
        }

        foreach (Match match in Regex.Matches(text, @"\bgithub\.com/[^\s<>\]\)\}""'`]+", RegexOptions.IgnoreCase))
        {
            var candidate = NormalizePossibleUrl(match.Value);
            if (!string.IsNullOrWhiteSpace(candidate) && IsPullRequestUrl(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsPullRequestUrl(string candidate)
    {
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            return false;

        var path = uri.AbsolutePath.ToLowerInvariant();
        return path.Contains("/pull/") ||
               path.Contains("/pulls/") ||
               path.Contains("/pull-request/") ||
               path.Contains("/pull-requests/") ||
               path.Contains("/merge_requests/") ||
               path.Contains("/merge-requests/");
    }

    private static string? NormalizePossibleUrl(string? value)
    {
        var trimmed = (value ?? string.Empty)
            .Trim()
            .TrimEnd('.', ',', ';', ':', ')', ']', '}');
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        if (trimmed.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            trimmed = $"https://{trimmed}";
        else if (trimmed.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase))
            trimmed = $"https://{trimmed}";

        return Uri.TryCreate(trimmed, UriKind.Absolute, out _)
            ? trimmed
            : null;
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

    private static string BuildFeaturePrompt(ProjectFeature feature, ChangeRequest? project, string additionalInstructions, CodexSettings settings)
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

    private static CodexBugScanResult Failed(string error) =>
        new(false, error, [], string.Empty);

    private static CodexBugFixResult FailedFix(string error) =>
        new(false, error, string.Empty, null);

    private static CodexFeatureImplementResult FailedFeature(string error) =>
        new(false, error, string.Empty);

    private static CodexTestCaseGenResult FailedTestCaseGen(string error) =>
        new(false, error, [], string.Empty);

    private static CodexProjectHealthResult FailedProjectHealth(string error) =>
        new(false, error, 0, string.Empty, string.Empty, "Medium", [], string.Empty);
}
