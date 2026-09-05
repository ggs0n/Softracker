using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WFHMonitor.AutomationService.Configuration;
using WFHMonitor.AutomationService.Services;

namespace WFHMonitor.Tests;

public sealed class CodexAppServerClientTests
{
    [Fact]
    public async Task OAuth_PersistsAcrossRestart_ReportsLimits_AndLogsOut()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(
            Path.GetTempPath(),
            $"wfhmonitor-fake-codex-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var statePath = Path.Combine(root, "account.state");
        var logPath = Path.Combine(root, "methods.log");
        var scriptPath = Path.Combine(root, "fake-app-server.ps1");
        await File.WriteAllTextAsync(
            scriptPath,
            FakeServerScript(statePath, logPath));

        try
        {
            await using (var first = CreateClient(scriptPath))
            {
                var disconnected =
                    await first.GetAccountStatusAsync();
                Assert.Equal("disconnected", disconnected.State);

                var login = await first.StartLoginAsync();
                Assert.StartsWith(
                    "https://auth.example.test/",
                    login.AuthUrl);
                var loginStatus = await WaitForLoginAsync(
                    first,
                    login.LoginId);
                Assert.Equal("completed", loginStatus.State);

                var connected = await first.GetAccountStatusAsync();
                Assert.Equal("connected", connected.State);
                Assert.Equal("plus", connected.PlanType);
                Assert.Equal(37, connected.UsedPercent);
                Assert.NotNull(connected.ResetsAt);
                await first.StopAsync(CancellationToken.None);
            }

            await using (var restarted = CreateClient(scriptPath))
            {
                var persisted =
                    await restarted.GetAccountStatusAsync();
                Assert.Equal("connected", persisted.State);

                await restarted.LogoutAsync();
                var loggedOut =
                    await restarted.GetAccountStatusAsync();
                Assert.Equal("disconnected", loggedOut.State);
                await restarted.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancelledTurn_IsInterrupted_AndTemporaryThreadIsDeleted()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(
            Path.GetTempPath(),
            $"wfhmonitor-fake-codex-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var statePath = Path.Combine(root, "account.state");
        var logPath = Path.Combine(root, "methods.log");
        var scriptPath = Path.Combine(root, "fake-app-server.ps1");
        await File.WriteAllTextAsync(statePath, "connected");
        await File.WriteAllTextAsync(
            scriptPath,
            FakeServerScript(statePath, logPath));

        try
        {
            await using var client = CreateClient(scriptPath);
            using var cancellation = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(1000));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => client.RunTurnAsync(
                    new CodexTurnRequest(
                        "wait for cancellation",
                        root,
                        false),
                    cancellation.Token));

            var methods = await File.ReadAllTextAsync(logPath);
            Assert.Contains("turn/interrupt", methods);
            Assert.Contains("thread/delete", methods);
            await client.StopAsync(CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ProviderImageGenerationItem_IsCaptured_AndThreadIsDeleted()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var root = Path.Combine(
            Path.GetTempPath(),
            $"wfhmonitor-fake-codex-image-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var statePath = Path.Combine(root, "account.state");
        var logPath = Path.Combine(root, "methods.log");
        var scriptPath = Path.Combine(root, "fake-app-server.ps1");
        await File.WriteAllTextAsync(statePath, "connected");
        await File.WriteAllTextAsync(
            scriptPath,
            FakeServerScript(statePath, logPath));

        try
        {
            await using var client = CreateClient(scriptPath);

            Assert.True(await client.SupportsImageGenerationAsync());
            var result = await client.RunTurnAsync(
                new CodexTurnRequest(
                    "image test",
                    root,
                    false));

            var image = Assert.Single(result.GeneratedImages ?? []);
            Assert.Equal("completed", image.Status);
            Assert.NotEmpty(image.Result);
            Assert.Equal("focused module prompt", image.RevisedPrompt);
            var methods = await File.ReadAllTextAsync(logPath);
            Assert.Contains("modelProvider/capabilities/read", methods);
            Assert.Contains("thread/delete", methods);
            await client.StopAsync(CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static CodexAppServerClient CreateClient(
        string executablePath) =>
        new(
            Options.Create(new CodexSettings
            {
                ExecutablePath = executablePath,
                Model = "gpt-5.6-sol",
                ReasoningEffort = "medium",
                StartupTimeoutSeconds = 10,
                RequestTimeoutSeconds = 10,
                LoginTimeoutSeconds = 10
            }),
            NullLogger<CodexAppServerClient>.Instance);

    private static async Task<WFHMonitor.Services.Interfaces.AiLoginStatus>
        WaitForLoginAsync(
            ICodexAppServerClient client,
            string loginId)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var status = await client.GetLoginStatusAsync(loginId);
            if (status.State != "pending")
                return status;
            await Task.Delay(20);
        }

        throw new TimeoutException(
            "The fake OAuth completion notification was not received.");
    }

    private static string FakeServerScript(
        string statePath,
        string logPath)
    {
        var escapedStatePath = statePath.Replace(
            "'",
            "''",
            StringComparison.Ordinal);
        var escapedLogPath = logPath.Replace(
            "'",
            "''",
            StringComparison.Ordinal);
        return $$"""
            param(
              [Parameter(ValueFromRemainingArguments = $true)]
              [string[]] $RemainingArguments
            )

            $statePath = '{{escapedStatePath}}'
            $logPath = '{{escapedLogPath}}'

            function Send-Json([object] $payload) {
              $json = $payload | ConvertTo-Json -Compress -Depth 20
              [Console]::Out.WriteLine($json)
              [Console]::Out.Flush()
            }

            while (($line = [Console]::In.ReadLine()) -ne $null) {
              if ([string]::IsNullOrWhiteSpace($line)) { continue }
              $message = $line | ConvertFrom-Json
              if ($null -eq $message.id) { continue }
              Add-Content -LiteralPath $logPath -Value $message.method

              switch ($message.method) {
                'initialize' {
                  Send-Json @{
                    id = $message.id
                    result = @{
                      userAgent = 'fake-codex'
                      protocolVersion = 2
                    }
                  }
                }
                'account/read' {
                  $account = $null
                  if (Test-Path -LiteralPath $statePath) {
                    $account = @{
                      type = 'chatgpt'
                      email = 'plus@example.test'
                      planType = 'plus'
                    }
                  }
                  Send-Json @{
                    id = $message.id
                    result = @{ account = $account }
                  }
                }
                'account/login/start' {
                  $loginId = 'login-1'
                  Send-Json @{
                    id = $message.id
                    result = @{
                      type = 'chatgpt'
                      loginId = $loginId
                      authUrl = 'https://auth.example.test/oauth'
                    }
                  }
                  Start-Sleep -Milliseconds 100
                  Set-Content -LiteralPath $statePath -Value 'connected'
                  Send-Json @{
                    method = 'account/login/completed'
                    params = @{
                      loginId = $loginId
                      success = $true
                      error = $null
                    }
                  }
                }
                'account/rateLimits/read' {
                  Send-Json @{
                    id = $message.id
                    result = @{
                      rateLimits = @{
                        primary = @{
                          usedPercent = 37
                          resetsAt = 1893456000
                        }
                        rateLimitReachedType = $null
                      }
                    }
                  }
                }
                'account/logout' {
                  Remove-Item -LiteralPath $statePath -Force -ErrorAction SilentlyContinue
                  Send-Json @{
                    id = $message.id
                    result = @{}
                  }
                }
                'modelProvider/capabilities/read' {
                  Send-Json @{
                    id = $message.id
                    result = @{
                      imageGeneration = $true
                      namespaceTools = $true
                      webSearch = $false
                    }
                  }
                }
                'account/login/cancel' {
                  Send-Json @{
                    id = $message.id
                    result = @{}
                  }
                }
                'thread/start' {
                  Send-Json @{
                    id = $message.id
                    result = @{
                      thread = @{ id = 'thread-1' }
                    }
                  }
                }
                'turn/start' {
                  Send-Json @{
                    id = $message.id
                    result = @{
                      turn = @{ id = 'turn-1' }
                    }
                  }
                  $prompt = $message.params.input[0].text
                  if ($prompt -eq 'image test') {
                    Send-Json @{
                      method = 'item/completed'
                      params = @{
                        threadId = 'thread-1'
                        turnId = 'turn-1'
                        item = @{
                          id = 'image-1'
                          type = 'imageGeneration'
                          status = 'completed'
                          result = 'iVBORw0KGgo='
                          revisedPrompt = 'focused module prompt'
                          savedPath = $null
                        }
                      }
                    }
                    Send-Json @{
                      method = 'item/completed'
                      params = @{
                        threadId = 'thread-1'
                        turnId = 'turn-1'
                        item = @{
                          id = 'message-1'
                          type = 'agentMessage'
                          text = 'Generated one image.'
                        }
                      }
                    }
                    Send-Json @{
                      method = 'turn/completed'
                      params = @{
                        threadId = 'thread-1'
                        turn = @{
                          id = 'turn-1'
                          status = 'completed'
                        }
                      }
                    }
                  }
                }
                'turn/interrupt' {
                  Send-Json @{
                    id = $message.id
                    result = @{}
                  }
                }
                'thread/delete' {
                  Send-Json @{
                    id = $message.id
                    result = @{}
                  }
                }
                default {
                  Send-Json @{
                    id = $message.id
                    error = @{
                      code = -32601
                      message = 'Method not implemented by fake server.'
                    }
                  }
                }
              }
            }
            """;
    }
}
