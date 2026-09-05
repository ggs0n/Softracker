using Microsoft.Extensions.Options;
using WFHMonitor.Configuration;

namespace WFHMonitor.Services;

public sealed class RepositoryWorkspaceCleanupService(
    IOptions<GitAutomationSettings> settings,
    IHostEnvironment environment,
    ILogger<RepositoryWorkspaceCleanupService> logger)
    : BackgroundService
{
    private readonly GitAutomationSettings _settings = settings.Value;
    private readonly string _root = Path.GetFullPath(
        Path.IsPathRooted(settings.Value.WorkspaceRoot)
            ? settings.Value.WorkspaceRoot
            : Path.Combine(
                environment.ContentRootPath,
                settings.Value.WorkspaceRoot));

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(
            Math.Max(1, _settings.CleanupIntervalMinutes)));
        do
        {
            CleanupExpiredDirectories();
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private void CleanupExpiredDirectories()
    {
        if (!Directory.Exists(_root))
            return;

        var cutoff = DateTime.UtcNow.AddHours(
            -Math.Max(0, _settings.WorkspaceRetentionHours));
        foreach (var directory in Directory.EnumerateDirectories(_root))
        {
            try
            {
                var resolved = Path.GetFullPath(directory);
                if (!resolved.StartsWith(
                        _root.TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar)
                        + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                if (Directory.GetLastWriteTimeUtc(resolved) > cutoff)
                    continue;
                Directory.Delete(resolved, recursive: true);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Unable to clean an expired AI repository workspace.");
            }
        }
    }
}
