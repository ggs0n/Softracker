using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WFHMonitor.Configuration;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public sealed class RepositoryWorkspaceService
    : IRepositoryWorkspaceService
{
    private static readonly Regex RepositoryPartPattern =
        new("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);
    private readonly IGitHubOAuthService _gitHubOAuth;
    private readonly GitAutomationSettings _settings;
    private readonly string _workspaceRoot;

    public RepositoryWorkspaceService(
        IGitHubOAuthService gitHubOAuth,
        IOptions<GitAutomationSettings> settings,
        IHostEnvironment environment)
    {
        _gitHubOAuth = gitHubOAuth;
        _settings = settings.Value;
        _workspaceRoot = ResolveRoot(
            _settings.WorkspaceRoot,
            environment.ContentRootPath);
    }

    public async Task<RepositoryWorkspace> PrepareAsync(
        ChangeRequest project,
        string requestingUserId,
        string workItemKey,
        CancellationToken cancellationToken = default)
    {
        return await PrepareCoreAsync(
            project,
            requestingUserId,
            workItemKey,
            createWorkBranch: true,
            cancellationToken);
    }

    public Task<RepositoryWorkspace> PrepareReadOnlyAsync(
        ChangeRequest project,
        string requestingUserId,
        string workItemKey,
        CancellationToken cancellationToken = default) =>
        PrepareCoreAsync(
            project,
            requestingUserId,
            workItemKey,
            createWorkBranch: false,
            cancellationToken);

    private async Task<RepositoryWorkspace> PrepareCoreAsync(
        ChangeRequest project,
        string requestingUserId,
        string workItemKey,
        bool createWorkBranch,
        CancellationToken cancellationToken)
    {
        var owner = RequireRepositoryPart(
            project.GitHubRepoOwner,
            "repository owner");
        var repository = RequireRepositoryPart(
            project.GitHubRepoName,
            "repository name");
        var baseBranch = RequireBranch(project.GitHubBranch);
        var token = await RequireTokenAsync(requestingUserId);

        Directory.CreateDirectory(_workspaceRoot);
        var jobDirectory = Path.GetFullPath(Path.Combine(
            _workspaceRoot,
            $"{Sanitize(workItemKey, 40)}-{Guid.NewGuid():N}"));
        EnsureInsideRoot(jobDirectory);
        Directory.CreateDirectory(jobDirectory);

        var repositoryDirectory = Path.Combine(
            jobDirectory,
            "repository");
        var cloneUrl = _settings.CloneUrlTemplate
            .Replace(
                "{owner}",
                owner,
                StringComparison.Ordinal)
            .Replace(
                "{repository}",
                repository,
                StringComparison.Ordinal);
        if (!Uri.TryCreate(
                cloneUrl,
                UriKind.Absolute,
                out _))
        {
            throw new InvalidOperationException(
                "GitAutomation:CloneUrlTemplate did not produce a valid absolute URL.");
        }
        await RunGitAsync(
            jobDirectory,
            token,
            cancellationToken,
            "clone",
            "--single-branch",
            "--branch",
            baseBranch,
            cloneUrl,
            repositoryDirectory);

        var workBranch =
            $"{_settings.BranchPrefix.Trim().TrimEnd('/')}/" +
            $"{Sanitize(workItemKey, 35)}-" +
            $"{Guid.NewGuid():N}"[..8];
        if (createWorkBranch)
        {
            await RunGitAsync(
                repositoryDirectory,
                null,
                cancellationToken,
                "checkout",
                "-b",
                workBranch);
            await RunGitAsync(
                repositoryDirectory,
                null,
                cancellationToken,
                "config",
                "user.name",
                _settings.CommitUserName);
            await RunGitAsync(
                repositoryDirectory,
                null,
                cancellationToken,
                "config",
                "user.email",
                _settings.CommitUserEmail);
        }

        return new RepositoryWorkspace(
            jobDirectory,
            repositoryDirectory,
            owner,
            repository,
            baseBranch,
            workBranch,
            requestingUserId);
    }

    public async Task<RepositoryPushResult> CommitAndPushAsync(
        RepositoryWorkspace workspace,
        string commitMessage,
        CancellationToken cancellationToken = default)
    {
        EnsureInsideRoot(workspace.RepositoryDirectory);
        var status = await RunGitAsync(
            workspace.RepositoryDirectory,
            null,
            cancellationToken,
            "status",
            "--porcelain");
        if (string.IsNullOrWhiteSpace(status))
            return new RepositoryPushResult(
                false,
                "Codex completed without changing repository files.");

        await RunGitAsync(
            workspace.RepositoryDirectory,
            null,
            cancellationToken,
            "add",
            "--all");
        await RunGitAsync(
            workspace.RepositoryDirectory,
            null,
            cancellationToken,
            "commit",
            "-m",
            commitMessage);

        var token = await RequireTokenAsync(
            workspace.RequestingUserId);
        await RunGitAsync(
            workspace.RepositoryDirectory,
            token,
            cancellationToken,
            "push",
            "--set-upstream",
            "origin",
            workspace.WorkBranch);
        return new RepositoryPushResult(
            true,
            status.Trim());
    }

    private async Task<string> RequireTokenAsync(string userId)
    {
        var token = await _gitHubOAuth.GetAccessTokenForUserAsync(
            userId);
        return string.IsNullOrWhiteSpace(token)
            ? throw new InvalidOperationException(
                "Connect a GitHub account with repository write access before running AI fixes.")
            : token;
    }

    private async Task<string> RunGitAsync(
        string workingDirectory,
        string? token,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        var resolvedWorkingDirectory =
            Path.GetFullPath(workingDirectory);
        EnsureInsideRoot(resolvedWorkingDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = _settings.GitExecutablePath,
            WorkingDirectory = resolvedWorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        string? askPassPath = null;
        if (!string.IsNullOrWhiteSpace(token))
        {
            askPassPath = CreateAskPassScript();
            startInfo.Environment["GIT_ASKPASS"] = askPassPath;
            startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
            startInfo.Environment["GCM_INTERACTIVE"] = "Never";
            startInfo.Environment["WFHMONITOR_GITHUB_TOKEN"] =
                token;
        }

        try
        {
            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    "The git process could not be started.");
            var outputTask = process.StandardOutput.ReadToEndAsync(
                cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(
                cancellationToken);
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(
                    Math.Max(1, _settings.CommandTimeoutSeconds)));
            using var linked = CancellationTokenSource
                .CreateLinkedTokenSource(
                    cancellationToken,
                    timeout.Token);
            await process.WaitForExitAsync(linked.Token);
            var output = await outputTask;
            var error = await errorTask;
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Git command failed: {SanitizeGitError(error)}");
            }

            return output;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(askPassPath))
            {
                try
                {
                    File.Delete(askPassPath);
                }
                catch
                {
                    // The script contains no credentials. Cleanup retries
                    // are left to the operating system.
                }
            }
        }
    }

    private static string CreateAskPassScript()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"wfhmonitor-git-{Guid.NewGuid():N}.cmd");
        File.WriteAllText(
            path,
            """
            @echo off
            echo %* | findstr /I "Username" >nul
            if %errorlevel%==0 (
              echo x-access-token
            ) else (
              echo %WFHMONITOR_GITHUB_TOKEN%
            )
            """,
            new UTF8Encoding(false));
        return path;
    }

    private void EnsureInsideRoot(string path)
    {
        var resolved = Path.GetFullPath(path);
        if (!resolved.StartsWith(
                _workspaceRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                resolved,
                _workspaceRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The requested repository workspace is outside the configured root.");
        }
    }

    private static string ResolveRoot(
        string configuredRoot,
        string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(configuredRoot))
            throw new InvalidOperationException(
                "GitAutomation:WorkspaceRoot is required.");
        return Path.GetFullPath(
            Path.IsPathRooted(configuredRoot)
                ? configuredRoot
                : Path.Combine(contentRoot, configuredRoot))
            .TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
    }

    private static string RequireRepositoryPart(
        string? value,
        string label)
    {
        var result = (value ?? string.Empty).Trim();
        if (!RepositoryPartPattern.IsMatch(result))
            throw new InvalidOperationException(
                $"A valid GitHub {label} is required.");
        return result;
    }

    private static string RequireBranch(string? value)
    {
        var branch = string.IsNullOrWhiteSpace(value)
            ? "main"
            : value.Trim();
        if (branch.Length > 150
            || branch.Contains("..", StringComparison.Ordinal)
            || branch.StartsWith("-", StringComparison.Ordinal)
            || branch.Any(character =>
                char.IsControl(character)
                || char.IsWhiteSpace(character)
                || "~^:?*[\\".Contains(character)))
        {
            throw new InvalidOperationException(
                "The configured GitHub base branch is invalid.");
        }

        return branch;
    }

    private static string Sanitize(string value, int maxLength)
    {
        var sanitized = Regex.Replace(
                value.ToLowerInvariant(),
                "[^a-z0-9._-]+",
                "-")
            .Trim('-', '.', '_');
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "work-item";
        return sanitized[..Math.Min(maxLength, sanitized.Length)];
    }

    private static string SanitizeGitError(string value)
    {
        var text = Regex.Replace(
                value ?? string.Empty,
                "https://[^\\s@]+@",
                "https://",
                RegexOptions.IgnoreCase)
            .Trim();
        if (string.IsNullOrWhiteSpace(text))
            return "git returned a non-zero exit code.";
        return text[..Math.Min(800, text.Length)];
    }
}
