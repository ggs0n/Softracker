using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WFHMonitor.Configuration;
using WFHMonitor.Models;
using WFHMonitor.Services;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Tests;

public sealed class GitAutomationTests
{
    [Fact]
    public async Task Workspace_Clones_Commits_AndPushesToLocalFakeRepository()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"wfhmonitor-git-tests-{Guid.NewGuid():N}");
        var remoteRoot = Path.Combine(root, "remotes");
        var bareRepository = Path.Combine(
            remoteRoot,
            "acme",
            "widget.git");
        var seed = Path.Combine(root, "seed");
        Directory.CreateDirectory(Path.GetDirectoryName(
            bareRepository)!);
        Directory.CreateDirectory(seed);

        try
        {
            RunGit(root, "init", "--bare", bareRepository);
            RunGit(seed, "init");
            RunGit(seed, "checkout", "-b", "main");
            RunGit(seed, "config", "user.name", "Test User");
            RunGit(
                seed,
                "config",
                "user.email",
                "test@example.test");
            await File.WriteAllTextAsync(
                Path.Combine(seed, "README.md"),
                "seed");
            RunGit(seed, "add", "--all");
            RunGit(seed, "commit", "-m", "seed");
            RunGit(seed, "remote", "add", "origin", bareRepository);
            RunGit(seed, "push", "origin", "main");

            var oauth = new Mock<IGitHubOAuthService>();
            oauth.Setup(service =>
                    service.GetAccessTokenForUserAsync("admin"))
                .ReturnsAsync("github-secret");
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(value => value.ContentRootPath)
                .Returns(root);
            var remoteTemplate =
                new Uri(remoteRoot + Path.DirectorySeparatorChar)
                    .AbsoluteUri
                + "{owner}/{repository}.git";
            var service = new RepositoryWorkspaceService(
                oauth.Object,
                Options.Create(new GitAutomationSettings
                {
                    WorkspaceRoot = Path.Combine(root, "workspaces"),
                    CloneUrlTemplate = remoteTemplate,
                    BranchPrefix = "automation/fix",
                    CommitUserName = "Automation Test",
                    CommitUserEmail = "automation@example.test",
                    CommandTimeoutSeconds = 30
                }),
                environment.Object);

            var workspace = await service.PrepareAsync(
                new ChangeRequest
                {
                    GitHubRepoOwner = "acme",
                    GitHubRepoName = "widget",
                    GitHubBranch = "main"
                },
                "admin",
                "BUG-42");
            await File.WriteAllTextAsync(
                Path.Combine(
                    workspace.RepositoryDirectory,
                    "fix.txt"),
                "fixed");

            var pushed = await service.CommitAndPushAsync(
                workspace,
                "fix: BUG-42");
            var noMoreChanges =
                await service.CommitAndPushAsync(
                    workspace,
                    "fix: no changes");

            Assert.True(pushed.HasChanges);
            Assert.False(noMoreChanges.HasChanges);
            Assert.DoesNotContain(
                "github-secret",
                await File.ReadAllTextAsync(Path.Combine(
                    workspace.RepositoryDirectory,
                    ".git",
                    "config")),
                StringComparison.Ordinal);
            RunGit(
                root,
                "--git-dir",
                bareRepository,
                "show-ref",
                $"refs/heads/{workspace.WorkBranch}");
        }
        finally
        {
            DeleteTestDirectory(root);
        }
    }

    [Fact]
    public async Task GitHubService_CreatesDraftPullRequestWithUserOAuth()
    {
        var handler = new PullRequestHandler();
        using var http = new HttpClient(handler);
        var oauth = new Mock<IGitHubOAuthService>();
        oauth.Setup(service =>
                service.GetAccessTokenForUserAsync("admin"))
            .ReturnsAsync("github-secret");
        var service = new GitHubService(
            http,
            Options.Create(new GitHubSettings
            {
                ApiBaseUrl = "https://api.github.test/",
                WebBaseUrl = "https://github.test/"
            }),
            oauth.Object);

        var result = await service.CreateDraftPullRequestAsync(
            "acme",
            "widget",
            "Fix BUG-42",
            "automation/fix/bug-42",
            "main",
            "Root cause and validation",
            "admin");

        Assert.Equal(84, result.Number);
        Assert.Equal(
            "https://github.test/acme/widget/pull/84",
            result.Url);
        Assert.Equal("github-secret", handler.BearerToken);
        Assert.True(handler.WasDraft);
        Assert.DoesNotContain(
            "github-secret",
            handler.RequestBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cleanup_RemovesOnlyExpiredWorkspaceDirectories()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            $"wfhmonitor-cleanup-tests-{Guid.NewGuid():N}");
        var expired = Path.Combine(root, "expired");
        var current = Path.Combine(root, "current");
        Directory.CreateDirectory(expired);
        Directory.CreateDirectory(current);
        Directory.SetLastWriteTimeUtc(
            expired,
            DateTime.UtcNow.AddHours(-2));
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.ContentRootPath)
            .Returns(root);
        var service = new RepositoryWorkspaceCleanupService(
            Options.Create(new GitAutomationSettings
            {
                WorkspaceRoot = root,
                WorkspaceRetentionHours = 1,
                CleanupIntervalMinutes = 60
            }),
            environment.Object,
            NullLogger<RepositoryWorkspaceCleanupService>.Instance);

        try
        {
            await service.StartAsync(CancellationToken.None);
            await Task.Delay(100);

            Assert.False(Directory.Exists(expired));
            Assert.True(Directory.Exists(current));
            await service.StopAsync(CancellationToken.None);
        }
        finally
        {
            DeleteTestDirectory(root);
        }
    }

    private static void RunGit(
        string workingDirectory,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        startInfo.Environment["GIT_CONFIG_GLOBAL"] =
            Path.Combine(workingDirectory, ".test-gitconfig");
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Unable to start git for the test.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(
            process.ExitCode == 0,
            $"git {string.Join(' ', arguments)} failed: {output} {error}");
    }

    private static void DeleteTestDirectory(string root)
    {
        if (!Directory.Exists(root))
            return;
        foreach (var file in Directory.EnumerateFiles(
                     root,
                     "*",
                     SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        foreach (var directory in Directory.EnumerateDirectories(
                         root,
                         "*",
                         SearchOption.AllDirectories)
                     .OrderByDescending(value => value.Length))
        {
            File.SetAttributes(directory, FileAttributes.Normal);
        }
        Directory.Delete(root, recursive: true);
    }

    private sealed class PullRequestHandler : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;
        public string? BearerToken { get; private set; }
        public bool WasDraft { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(
                "/repos/acme/widget/pulls",
                request.RequestUri?.AbsolutePath);
            BearerToken =
                request.Headers.Authorization?.Parameter;
            Assert.Equal(
                "Bearer",
                request.Headers.Authorization?.Scheme);
            RequestBody = await request.Content!
                .ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(RequestBody);
            WasDraft = document.RootElement
                .GetProperty("draft")
                .GetBoolean();

            return new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(
                    """
                    {
                      "number": 84,
                      "html_url": "https://github.test/acme/widget/pull/84"
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
