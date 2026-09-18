using System.Diagnostics;
using Microsoft.Extensions.Options;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public sealed class CodexAuthService(
    IOptions<CodexSettings> settings,
    ILogger<CodexAuthService> logger) : ICodexAuthService
{
    public async Task<CodexAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var cliPath = ResolveCliPath(settings.Value.CliPath);
        if (cliPath is null)
            return new(false, false, "Codex CLI is not installed.");

        try
        {
            var result = await RunCapturedAsync(cliPath, ["login", "status"], TimeSpan.FromSeconds(10), cancellationToken);
            var message = FirstUsefulLine(result.StandardOutput, result.StandardError);
            var authenticationDetail = $"{result.StandardOutput}\n{result.StandardError}";
            var isChatGptLogin = result.ExitCode == 0 &&
                authenticationDetail.Contains("ChatGPT", StringComparison.OrdinalIgnoreCase);
            return new(
                true,
                result.ExitCode == 0,
                string.IsNullOrWhiteSpace(message)
                    ? result.ExitCode == 0 ? "Codex is authenticated." : "Codex is not authenticated."
                    : message,
                isChatGptLogin);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to read Codex authentication status.");
            return new(false, false, "Unable to start Codex CLI.");
        }
    }

    public async Task<CodexLoginStartResult> StartLoginAsync(CancellationToken cancellationToken = default)
    {
        var cliPath = ResolveCliPath(settings.Value.CliPath);
        if (cliPath is null)
            return new(false, "Codex CLI is not installed. Install it before connecting ChatGPT.");

        var current = await GetStatusAsync(cancellationToken);
        if (current.IsAuthenticated && current.IsChatGptLogin)
            return new(true, "Codex is already connected with ChatGPT.");

        if (current.IsAuthenticated)
        {
            var disconnected = await LogoutAsync(cancellationToken);
            if (!disconnected)
                return new(false, "Unable to replace the existing Codex authentication with Sign in with ChatGPT.");
        }

        try
        {
            var startInfo = BuildStartInfo(cliPath, ["login"], captureOutput: false);
            using var process = Process.Start(startInfo);
            if (process is null)
                return new(false, "Unable to start Codex login.");

            await Task.Delay(750, cancellationToken);
            if (process.HasExited && process.ExitCode != 0)
                return new(false, "Codex login exited before authentication could begin.");

            return new(true, "Codex opened the ChatGPT sign-in flow. Complete it in the browser, then refresh this page.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to start Codex ChatGPT login.");
            return new(false, "Unable to start Codex ChatGPT login.");
        }
    }

    public async Task<bool> LogoutAsync(CancellationToken cancellationToken = default)
    {
        var cliPath = ResolveCliPath(settings.Value.CliPath);
        if (cliPath is null)
            return false;

        try
        {
            var result = await RunCapturedAsync(cliPath, ["logout"], TimeSpan.FromSeconds(10), cancellationToken);
            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unable to log out of Codex.");
            return false;
        }
    }

    private static async Task<CodexProcessResult> RunCapturedAsync(
        string cliPath,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = BuildStartInfo(cliPath, arguments, captureOutput: true);
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Unable to start Codex CLI.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
            throw new TimeoutException("Codex CLI did not respond in time.");
        }

        return new(process.ExitCode, (await stdoutTask).Trim(), (await stderrTask).Trim());
    }

    private static ProcessStartInfo BuildStartInfo(
        string cliPath,
        IReadOnlyList<string> arguments,
        bool captureOutput)
    {
        var usePowerShell = cliPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);
        var useCommandShell = cliPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                              cliPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo
        {
            FileName = usePowerShell ? "powershell" : useCommandShell ? "cmd.exe" : cliPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput
        };

        if (usePowerShell)
        {
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(cliPath);
        }
        else if (useCommandShell)
        {
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/s");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(cliPath);
        }

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return startInfo;
    }

    private static string? ResolveCliPath(string? configuredPath)
    {
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("CODEX_CLI_PATH"),
            configuredPath
        };

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(appData))
            {
                candidates.Add(Path.Combine(appData, "npm", "codex.cmd"));
                candidates.Add(Path.Combine(appData, "npm", "codex.ps1"));
            }

            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                var extensionsDirectory = Path.Combine(userProfile, ".vscode", "extensions");
                if (Directory.Exists(extensionsDirectory))
                {
                    candidates.AddRange(Directory
                        .EnumerateFiles(extensionsDirectory, "codex.exe", SearchOption.AllDirectories)
                        .Where(path => path.Contains("openai.chatgpt-", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase));
                }
            }
        }

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;
            var expanded = Environment.ExpandEnvironmentVariables(candidate.Trim());
            if (Path.IsPathRooted(expanded) && File.Exists(expanded))
                return expanded;
        }

        return string.IsNullOrWhiteSpace(configuredPath) ? "codex" : configuredPath.Trim();
    }

    private static string FirstUsefulLine(params string[] values) =>
        values
            .SelectMany(value => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))
        ?? string.Empty;

    private sealed record CodexProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
