using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace WFHMonitor.Desktop;

public partial class MainWindow : Window
{
    private Process? _webProcess;
    private Uri? _localBaseUri;
    private CancellationTokenSource? _startupCancellation;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await StartWorkspaceAsync();
    }

    private async Task StartWorkspaceAsync()
    {
        StopWebProcess();
        _startupCancellation?.Cancel();
        _startupCancellation?.Dispose();
        _startupCancellation = new CancellationTokenSource();

        RetryButton.Visibility = Visibility.Collapsed;
        LoadingProgress.Visibility = Visibility.Visible;
        LoadingMessage.Text = "Starting your local workspace…";
        LoadingPanel.Visibility = Visibility.Visible;
        Browser.Visibility = Visibility.Collapsed;

        try
        {
            _localBaseUri = new Uri("http://localhost:5227");
            if (!await IsHealthyAsync(_localBaseUri, _startupCancellation.Token))
            {
                var executablePath = FindWebApplication();
                _webProcess = StartWebApplication(executablePath, _localBaseUri);
            }

            LoadingMessage.Text = "Preparing the database and background scanners…";
            await WaitForHealthAsync(_localBaseUri, _webProcess, _startupCancellation.Token);

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Softracker",
                "WebView2");
            Directory.CreateDirectory(userDataFolder);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await Browser.EnsureCoreWebView2Async(environment);
            Browser.CoreWebView2.NewWindowRequested += Browser_NewWindowRequested;
            Browser.Source = new Uri(_localBaseUri, "/Auth/Login");
            Browser.Visibility = Visibility.Visible;
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            // The window is closing or a retry replaced this startup attempt.
        }
        catch (Exception ex)
        {
            StopWebProcess();
            LoadingProgress.Visibility = Visibility.Collapsed;
            RetryButton.Visibility = Visibility.Visible;
            LoadingMessage.Text = ex.Message;
        }
    }

    private static string FindWebApplication()
    {
        var packagedPath = Path.Combine(AppContext.BaseDirectory, "webapp", "WFHMonitor.exe");
        if (File.Exists(packagedPath))
            return packagedPath;

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var debugPath = Path.Combine(current.FullName, "WFHMonitor", "bin", "Debug", "net8.0", "WFHMonitor.exe");
            if (File.Exists(debugPath))
                return debugPath;

            var releasePath = Path.Combine(current.FullName, "WFHMonitor", "bin", "Release", "net8.0", "WFHMonitor.exe");
            if (File.Exists(releasePath))
                return releasePath;
            current = current.Parent;
        }

        throw new FileNotFoundException(
            "The WFHMonitor web application was not found. Run publish-desktop.ps1 to create the complete desktop package.");
    }

    private static Process StartWebApplication(string executablePath, Uri baseUri)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add(baseUri.ToString().TrimEnd('/'));
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["SOFTRACKER_DESKTOP"] = "1";

        var process = Process.Start(startInfo);
        return process ?? throw new InvalidOperationException("Unable to start the local Softracker service.");
    }

    private static async Task<bool> IsHealthyAsync(Uri baseUri, CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await http.GetAsync(new Uri(baseUri, "/health"), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static async Task WaitForHealthAsync(Uri baseUri, Process? process, CancellationToken cancellationToken)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var healthUri = new Uri(baseUri, "/health");
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process is not null && process.HasExited)
                throw new InvalidOperationException($"The local Softracker service stopped with exit code {process.ExitCode}.");

            try
            {
                using var response = await http.GetAsync(healthUri, cancellationToken);
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch (HttpRequestException)
            {
                // Startup can briefly refuse connections while migrations are applied.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Retry a slow health request until the overall deadline expires.
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException("Softracker did not finish starting within 60 seconds. Check the database connection in webapp\\appsettings.json.");
    }

    private void Browser_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
            LoadingMessage.Text = $"Unable to initialize the embedded browser: {e.InitializationException?.Message}";
    }

    private void Browser_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) || _localBaseUri is null)
            return;

        if (target.Host.Equals(_localBaseUri.Host, StringComparison.OrdinalIgnoreCase) ||
            target.Host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (target.Scheme is "http" or "https")
        {
            e.Cancel = true;
            Process.Start(new ProcessStartInfo(target.ToString()) { UseShellExecute = true });
        }
    }

    private static void Browser_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) && target.Scheme is "http" or "https")
            Process.Start(new ProcessStartInfo(target.ToString()) { UseShellExecute = true });
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e) => await StartWorkspaceAsync();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _startupCancellation?.Cancel();
        StopWebProcess();
    }

    private void StopWebProcess()
    {
        if (_webProcess is null)
            return;

        try
        {
            if (!_webProcess.HasExited)
                _webProcess.Kill(entireProcessTree: true);
        }
        catch
        {
            // The process may already have exited during application shutdown.
        }
        finally
        {
            _webProcess.Dispose();
            _webProcess = null;
        }
    }
}
