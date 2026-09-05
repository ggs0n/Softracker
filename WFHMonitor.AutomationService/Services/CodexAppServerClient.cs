using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using WFHMonitor.AutomationService.Configuration;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.AutomationService.Services;

public sealed class CodexAppServerClient :
    ICodexAppServerClient,
    IHostedService,
    IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly CodexSettings _settings;
    private readonly ILogger<CodexAppServerClient> _logger;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly SemaphoreSlim _turnLock = new(1, 1);
    private readonly ConcurrentDictionary<long, TaskCompletionSource<JsonElement>>
        _pendingRequests = new();
    private readonly ConcurrentDictionary<string, AiLoginStatus>
        _loginStatuses = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TurnState>
        _turns = new(StringComparer.Ordinal);

    private Process? _process;
    private StreamWriter? _writer;
    private CancellationTokenSource? _processCts;
    private Task? _readLoop;
    private Task? _errorLoop;
    private long _nextRequestId;
    private string? _lastStartError;
    private bool _stopping;

    public CodexAppServerClient(
        IOptions<CodexSettings> settings,
        ILogger<CodexAppServerClient> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public string Model => _settings.Model;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureStartedAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _lastStartError = UserSafeStartError(exception);
            _logger.LogWarning(
                exception,
                "Codex App Server is not available at startup.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopping = true;
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            await StopProcessAsync();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task<AiAccountStatus> GetAccountStatusAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureStartedAsync(cancellationToken);
            var accountResponse = await SendRequestAsync(
                "account/read",
                new { refreshToken = false },
                RequestTimeout(),
                cancellationToken);
            var account = accountResponse.TryGetProperty("account", out var value)
                && value.ValueKind == JsonValueKind.Object
                ? value
                : default;

            if (account.ValueKind != JsonValueKind.Object)
            {
                return new AiAccountStatus(
                    "disconnected",
                    null,
                    null,
                    Model,
                    null,
                    null,
                    null,
                    "Connect a ChatGPT account to use AI automation.");
            }

            var type = StringProperty(account, "type");
            if (!string.Equals(type, "chatgpt", StringComparison.Ordinal))
            {
                return new AiAccountStatus(
                    "wrongAuthentication",
                    null,
                    null,
                    Model,
                    null,
                    null,
                    null,
                    "Codex is not authenticated with ChatGPT OAuth.");
            }

            var rateLimit = await TryReadRateLimitAsync(cancellationToken);
            return new AiAccountStatus(
                "connected",
                StringProperty(account, "email"),
                StringProperty(account, "planType"),
                Model,
                rateLimit.UsedPercent,
                rateLimit.ResetsAt,
                rateLimit.LimitReachedType,
                rateLimit.LimitReachedType is null
                    ? null
                    : "The connected ChatGPT account has reached its Codex usage limit.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Unable to read the Codex account status.");
            return new AiAccountStatus(
                "unavailable",
                null,
                null,
                Model,
                null,
                null,
                null,
                _lastStartError ?? "Codex App Server is unavailable.");
        }
    }

    public async Task<AiLoginStartResult> StartLoginAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureStartedAsync(cancellationToken);
        var response = await SendRequestAsync(
            "account/login/start",
            new
            {
                type = "chatgpt",
                useHostedLoginSuccessPage = true,
                appBrand = "chatgpt"
            },
            TimeSpan.FromSeconds(_settings.LoginTimeoutSeconds),
            cancellationToken);
        var loginId = RequiredString(response, "loginId");
        var authUrl = RequiredString(response, "authUrl");
        _loginStatuses[loginId] = new AiLoginStatus(
            loginId,
            "pending",
            null);
        return new AiLoginStartResult(loginId, authUrl);
    }

    public Task<AiLoginStatus> GetLoginStatusAsync(
        string loginId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(loginId))
            throw new ArgumentException("A login ID is required.", nameof(loginId));

        return Task.FromResult(
            _loginStatuses.TryGetValue(loginId, out var status)
                ? status
                : new AiLoginStatus(
                    loginId,
                    "unknown",
                    "The login attempt was not found."));
    }

    public async Task CancelLoginAsync(
        string loginId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(loginId))
            throw new ArgumentException("A login ID is required.", nameof(loginId));

        await SendRequestAsync(
            "account/login/cancel",
            new { loginId },
            RequestTimeout(),
            cancellationToken);
        _loginStatuses[loginId] = new AiLoginStatus(
            loginId,
            "cancelled",
            "Login was cancelled.");
    }

    public async Task LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        await SendRequestAsync(
            "account/logout",
            null,
            RequestTimeout(),
            cancellationToken);
    }

    public async Task<bool> SupportsImageGenerationAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureChatGptAccountAsync(cancellationToken);
        var response = await SendRequestAsync(
            "modelProvider/capabilities/read",
            new { },
            RequestTimeout(),
            cancellationToken);
        return response.TryGetProperty(
                   "imageGeneration",
                   out var supported)
               && supported.ValueKind == JsonValueKind.True;
    }

    public async Task<CodexTurnResult> RunTurnAsync(
        CodexTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new ArgumentException("A prompt is required.", nameof(request));

        var workingDirectory = ValidateWorkingDirectory(
            request.WorkingDirectory);
        var turnTimeout = request.TimeoutSeconds is > 0
            ? TimeSpan.FromSeconds(request.TimeoutSeconds.Value)
            : RequestTimeout();
        await _turnLock.WaitAsync(cancellationToken);
        string? threadId = null;
        try
        {
            await EnsureChatGptAccountAsync(cancellationToken);
            var threadResponse = await SendRequestAsync(
                "thread/start",
                new
                {
                    model = Model,
                    cwd = workingDirectory,
                    approvalPolicy = "never",
                    sandbox = request.AllowWrites
                        ? "workspace-write"
                        : "read-only",
                    ephemeral = false,
                    serviceName = "wfhmonitor_automation"
                },
                RequestTimeout(),
                cancellationToken);
            threadId = RequiredString(
                threadResponse.GetProperty("thread"),
                "id");

            var turnState = new TurnState(threadId);
            _turns[threadId] = turnState;
            var sandboxPolicy = request.AllowWrites
                ? new
                {
                    type = "workspaceWrite",
                    writableRoots = new[] { workingDirectory },
                    networkAccess = false,
                    excludeTmpdirEnvVar = true,
                    excludeSlashTmp = true
                }
                : (object)new
                {
                    type = "readOnly",
                    networkAccess = false
                };
            var turnParams = new JsonObject
            {
                ["threadId"] = threadId,
                ["input"] = JsonSerializer.SerializeToNode(
                    new[]
                    {
                        new
                        {
                            type = "text",
                            text = request.Prompt
                        }
                    },
                    JsonOptions),
                ["cwd"] = workingDirectory,
                ["approvalPolicy"] = "never",
                ["sandboxPolicy"] = JsonSerializer.SerializeToNode(
                    sandboxPolicy,
                    JsonOptions),
                ["model"] = Model,
                ["effort"] = _settings.ReasoningEffort,
                ["summary"] = "concise"
            };
            if (request.OutputSchema is not null)
                turnParams["outputSchema"] = request.OutputSchema.DeepClone();

            var turnResponse = await SendRequestAsync(
                "turn/start",
                turnParams,
                turnTimeout,
                cancellationToken);
            var turnId = RequiredString(
                turnResponse.GetProperty("turn"),
                "id");
            turnState.TurnId = turnId;

            using var timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            timeoutCts.CancelAfter(turnTimeout);
            var result = await turnState.Completion.Task.WaitAsync(
                timeoutCts.Token);
            if (!string.Equals(
                    result.Status,
                    "completed",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    result.Error ?? "Codex did not complete the AI operation.");
            }

            if (string.IsNullOrWhiteSpace(turnState.OutputText)
                && turnState.GeneratedImages.Count == 0)
                throw new InvalidOperationException(
                    "Codex completed without a final response.");

            return new CodexTurnResult(
                turnState.OutputText,
                turnState.Commands.ToArray(),
                turnState.ChangedFiles
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                turnState.GeneratedImages.ToArray());
        }
        catch (OperationCanceledException)
        {
            if (threadId is not null
                && _turns.TryGetValue(threadId, out var state)
                && state.TurnId is not null)
            {
                await TryInterruptTurnAsync(
                    threadId,
                    state.TurnId,
                    CancellationToken.None);
            }

            throw;
        }
        finally
        {
            if (threadId is not null)
            {
                _turns.TryRemove(threadId, out _);
                await TryDeleteThreadAsync(threadId, CancellationToken.None);
            }

            _turnLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stopping = true;
        await StopProcessAsync();
        _lifecycleLock.Dispose();
        _writeLock.Dispose();
        _turnLock.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task EnsureChatGptAccountAsync(
        CancellationToken cancellationToken)
    {
        var status = await GetAccountStatusAsync(cancellationToken);
        if (!string.Equals(
                status.State,
                "connected",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A ChatGPT OAuth account is not connected.");
        }

        if (status.LimitReachedType is not null)
            throw new InvalidOperationException(
                "The connected ChatGPT account has reached its Codex usage limit.");
    }

    private async Task EnsureStartedAsync(
        CancellationToken cancellationToken)
    {
        if (IsRunning())
            return;
        if (_stopping)
            throw new InvalidOperationException(
                "Codex App Server is stopping.");

        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (IsRunning())
                return;

            await StopProcessAsync();
            var startInfo = CreateStartInfo(_settings.ExecutablePath);
            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            if (!process.Start())
                throw new InvalidOperationException(
                    "Unable to start Codex App Server.");

            _process = process;
            _writer = process.StandardInput;
            _writer.AutoFlush = true;
            _processCts = new CancellationTokenSource();
            _readLoop = ReadLoopAsync(
                process.StandardOutput,
                _processCts.Token);
            _errorLoop = DrainErrorAsync(
                process.StandardError,
                _processCts.Token);

            using var startupCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            startupCts.CancelAfter(
                TimeSpan.FromSeconds(_settings.StartupTimeoutSeconds));
            await SendCoreAsync(
                "initialize",
                new
                {
                    clientInfo = new
                    {
                        name = "wfhmonitor",
                        title = "WFHMonitor",
                        version = "1.0.0"
                    },
                    capabilities = new
                    {
                        experimentalApi = false
                    }
                },
                startupCts.Token);
            await SendNotificationAsync(
                "initialized",
                new { },
                startupCts.Token);
            _lastStartError = null;
        }
        catch (Exception exception)
        {
            _lastStartError = UserSafeStartError(exception);
            await StopProcessAsync();
            throw;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private bool IsRunning() =>
        _process is { HasExited: false } && _writer is not null;

    private async Task<JsonElement> SendRequestAsync(
        string method,
        object? parameters,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await EnsureStartedAsync(cancellationToken);
        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        timeoutCts.CancelAfter(timeout);
        return await SendCoreAsync(
            method,
            parameters,
            timeoutCts.Token);
    }

    private async Task<JsonElement> SendCoreAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        var requestId = Interlocked.Increment(ref _nextRequestId);
        var completion =
            new TaskCompletionSource<JsonElement>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingRequests.TryAdd(requestId, completion))
            throw new InvalidOperationException(
                "Unable to register a Codex request.");

        try
        {
            await WriteMessageAsync(
                new
                {
                    method,
                    id = requestId,
                    @params = parameters
                },
                cancellationToken);
            using var registration = cancellationToken.Register(
                () => completion.TrySetCanceled(cancellationToken));
            return await completion.Task;
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    private Task SendNotificationAsync(
        string method,
        object parameters,
        CancellationToken cancellationToken) =>
        WriteMessageAsync(
            new
            {
                method,
                @params = parameters
            },
            cancellationToken);

    private async Task WriteMessageAsync(
        object message,
        CancellationToken cancellationToken)
    {
        var writer = _writer
            ?? throw new InvalidOperationException(
                "Codex App Server is not running.");
        var json = JsonSerializer.Serialize(message, JsonOptions);
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await writer.WriteLineAsync(
                json.AsMemory(),
                cancellationToken);
            await writer.FlushAsync(cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                    break;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var idElement)
                    && idElement.TryGetInt64(out var requestId)
                    && _pendingRequests.TryGetValue(
                        requestId,
                        out var pending))
                {
                    if (root.TryGetProperty("error", out var error))
                    {
                        pending.TrySetException(
                            new InvalidOperationException(
                                SafeRpcError(error)));
                    }
                    else if (root.TryGetProperty("result", out var result))
                    {
                        pending.TrySetResult(result.Clone());
                    }

                    continue;
                }

                if (root.TryGetProperty("method", out var methodElement))
                    HandleNotification(
                        methodElement.GetString(),
                        root.TryGetProperty("params", out var parameters)
                            ? parameters
                            : default);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (!_stopping)
            {
                _logger.LogWarning(
                    exception,
                    "The Codex App Server response stream stopped.");
            }
        }
        finally
        {
            FailPendingRequests(
                new InvalidOperationException(
                    "Codex App Server stopped unexpectedly."));
        }
    }

    private async Task DrainErrorAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested
                   && await reader.ReadLineAsync(cancellationToken) is not null)
            {
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            _logger.LogDebug(
                exception,
                "The Codex App Server diagnostic stream stopped.");
        }
    }

    private void HandleNotification(
        string? method,
        JsonElement parameters)
    {
        switch (method)
        {
            case "account/login/completed":
                HandleLoginCompleted(parameters);
                break;
            case "item/completed":
                HandleItemCompleted(parameters);
                break;
            case "turn/completed":
                HandleTurnCompleted(parameters);
                break;
        }
    }

    private void HandleLoginCompleted(JsonElement parameters)
    {
        var loginId = StringProperty(parameters, "loginId");
        if (string.IsNullOrWhiteSpace(loginId))
            return;
        var success = parameters.TryGetProperty("success", out var value)
                      && value.ValueKind == JsonValueKind.True;
        _loginStatuses[loginId] = new AiLoginStatus(
            loginId,
            success ? "completed" : "failed",
            success ? null : StringProperty(parameters, "error"));
    }

    private void HandleItemCompleted(JsonElement parameters)
    {
        var threadId = StringProperty(parameters, "threadId");
        if (threadId is null
            || !_turns.TryGetValue(threadId, out var state)
            || !parameters.TryGetProperty("item", out var item))
        {
            return;
        }

        var type = StringProperty(item, "type");
        switch (type)
        {
            case "agentMessage":
                state.OutputText = StringProperty(item, "text")
                                   ?? state.OutputText;
                break;
            case "commandExecution":
                state.Commands.Add(
                    new CodexCommandExecution(
                        StringProperty(item, "command") ?? string.Empty,
                        IntProperty(item, "exitCode"),
                        TrimOutput(StringProperty(item, "aggregatedOutput"))));
                break;
            case "fileChange":
                if (item.TryGetProperty("changes", out var changes)
                    && changes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var change in changes.EnumerateArray())
                    {
                        var path = StringProperty(change, "path");
                        if (!string.IsNullOrWhiteSpace(path))
                            state.ChangedFiles.Add(path);
                    }
                }

                break;
            case "imageGeneration":
                state.GeneratedImages.Add(
                    new CodexGeneratedImage(
                        StringProperty(item, "status") ?? "failed",
                        StringProperty(item, "result") ?? string.Empty,
                        StringProperty(item, "revisedPrompt"),
                        StringProperty(item, "savedPath")));
                break;
        }
    }

    private void HandleTurnCompleted(JsonElement parameters)
    {
        var threadId = StringProperty(parameters, "threadId");
        if (threadId is null
            || !_turns.TryGetValue(threadId, out var state)
            || !parameters.TryGetProperty("turn", out var turn))
        {
            return;
        }

        var status = StringProperty(turn, "status") ?? "failed";
        string? error = null;
        if (turn.TryGetProperty("error", out var errorElement)
            && errorElement.ValueKind == JsonValueKind.Object)
        {
            error = StringProperty(errorElement, "message")
                    ?? "Codex reported an error.";
        }

        state.Completion.TrySetResult(
            new TurnCompletion(status, error));
    }

    private async Task<(int? UsedPercent, DateTimeOffset? ResetsAt,
        string? LimitReachedType)> TryReadRateLimitAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await SendRequestAsync(
                "account/rateLimits/read",
                null,
                RequestTimeout(),
                cancellationToken);
            if (!response.TryGetProperty("rateLimits", out var limits))
                return default;

            int? usedPercent = null;
            DateTimeOffset? resetsAt = null;
            if (limits.TryGetProperty("primary", out var primary)
                && primary.ValueKind == JsonValueKind.Object)
            {
                usedPercent = IntProperty(primary, "usedPercent");
                var unixSeconds = LongProperty(primary, "resetsAt");
                if (unixSeconds.HasValue)
                {
                    resetsAt =
                        DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value);
                }
            }

            return (
                usedPercent,
                resetsAt,
                StringProperty(limits, "rateLimitReachedType"));
        }
        catch (Exception exception)
        {
            _logger.LogDebug(
                exception,
                "Codex rate-limit information is unavailable.");
            return default;
        }
    }

    private async Task TryInterruptTurnAsync(
        string threadId,
        string turnId,
        CancellationToken cancellationToken)
    {
        try
        {
            await SendRequestAsync(
                "turn/interrupt",
                new { threadId, turnId },
                RequestTimeout(),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(
                exception,
                "Unable to interrupt Codex turn {TurnId}.",
                turnId);
        }
    }

    private async Task TryDeleteThreadAsync(
        string threadId,
        CancellationToken cancellationToken)
    {
        try
        {
            await SendRequestAsync(
                "thread/delete",
                new { threadId },
                RequestTimeout(),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(
                exception,
                "Unable to delete temporary Codex thread {ThreadId}.",
                threadId);
        }
    }

    private async Task StopProcessAsync()
    {
        _processCts?.Cancel();
        var process = _process;
        _process = null;
        _writer = null;

        if (process is { HasExited: false })
        {
            try
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            catch (Exception exception)
            {
                _logger.LogDebug(
                    exception,
                    "Unable to stop the Codex App Server process cleanly.");
            }
        }

        process?.Dispose();
        _processCts?.Dispose();
        _processCts = null;
        FailPendingRequests(
            new InvalidOperationException(
                "Codex App Server was stopped."));
    }

    private void FailPendingRequests(Exception exception)
    {
        foreach (var request in _pendingRequests.Values)
            request.TrySetException(exception);
    }

    private TimeSpan RequestTimeout() =>
        TimeSpan.FromSeconds(_settings.RequestTimeoutSeconds);

    private static string ValidateWorkingDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                "A Codex working directory is required.");
        var fullPath = Path.GetFullPath(value);
        if (!Directory.Exists(fullPath))
            throw new DirectoryNotFoundException(
                "The Codex working directory does not exist.");
        return fullPath;
    }

    private static ProcessStartInfo CreateStartInfo(string configuredPath)
    {
        var resolvedPath = ResolveExecutable(configuredPath);
        var extension = Path.GetExtension(resolvedPath);
        var startInfo = new ProcessStartInfo
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (OperatingSystem.IsWindows()
            && extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName =
                Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe";
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(resolvedPath);
        }
        else if (OperatingSystem.IsWindows()
                 && extension.Equals(
                     ".ps1",
                     StringComparison.OrdinalIgnoreCase))
        {
            startInfo.FileName = "powershell.exe";
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(resolvedPath);
        }
        else
        {
            startInfo.FileName = resolvedPath;
        }

        startInfo.ArgumentList.Add("app-server");
        startInfo.ArgumentList.Add("--listen");
        startInfo.ArgumentList.Add("stdio://");
        return startInfo;
    }

    private static string ResolveExecutable(string configuredPath)
    {
        var candidate = string.IsNullOrWhiteSpace(configuredPath)
            ? "codex"
            : configuredPath.Trim();
        if (Path.IsPathRooted(candidate))
        {
            if (!File.Exists(candidate))
                throw new FileNotFoundException(
                    "The configured Codex executable was not found.");
            return candidate;
        }

        var extensions = OperatingSystem.IsWindows()
            ? new[] { ".exe", ".cmd", ".ps1", string.Empty }
            : new[] { string.Empty };
        foreach (var directory in (
                     Environment.GetEnvironmentVariable("PATH")
                     ?? string.Empty)
                 .Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            foreach (var extension in extensions)
            {
                var path = Path.Combine(
                    directory,
                    candidate.EndsWith(
                        extension,
                        StringComparison.OrdinalIgnoreCase)
                        ? candidate
                        : $"{candidate}{extension}");
                if (File.Exists(path))
                    return path;
            }
        }

        return candidate;
    }

    private static string RequiredString(
        JsonElement element,
        string name) =>
        StringProperty(element, name)
        ?? throw new InvalidOperationException(
            $"Codex response omitted {name}.");

    private static string? StringProperty(
        JsonElement element,
        string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? IntProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.TryGetInt32(out var result)
            ? result
            : null;

    private static long? LongProperty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.TryGetInt64(out var result)
            ? result
            : null;

    private static string SafeRpcError(JsonElement error)
    {
        var code = IntProperty(error, "code");
        var message = StringProperty(error, "message");
        return code.HasValue
            ? $"Codex request failed ({code.Value}): {message ?? "unknown error"}"
            : $"Codex request failed: {message ?? "unknown error"}";
    }

    private static string UserSafeStartError(Exception exception) =>
        exception is FileNotFoundException
            ? "Codex CLI was not found. Configure Codex:ExecutablePath."
            : "Codex App Server could not be started.";

    private static string? TrimOutput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= 2_000
            ? trimmed
            : trimmed[^2_000..];
    }

    private sealed class TurnState(string threadId)
    {
        public string ThreadId { get; } = threadId;
        public string? TurnId { get; set; }
        public string OutputText { get; set; } = string.Empty;
        public List<CodexCommandExecution> Commands { get; } = [];
        public List<string> ChangedFiles { get; } = [];
        public List<CodexGeneratedImage> GeneratedImages { get; } = [];
        public TaskCompletionSource<TurnCompletion> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record TurnCompletion(
        string Status,
        string? Error);
}
