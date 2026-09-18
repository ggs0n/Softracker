using System.Text.RegularExpressions;

namespace WFHMonitor.Services;

internal static partial class ScreenshotFileHelper
{
    private static readonly HashSet<string> SupportedExtensions =
        new([".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp"], StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedImageExtension(string? extension) =>
        !string.IsNullOrWhiteSpace(extension) && SupportedExtensions.Contains(extension);

    public static string ResolvePath(string path, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (Path.IsPathRooted(expanded))
            return expanded;

        try
        {
            return Path.GetFullPath(Path.Combine(contentRootPath, expanded));
        }
        catch
        {
            return string.Empty;
        }
    }

    public static List<string> ExtractCandidatePaths(string? responseText, string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return [];

        var files = new List<string>();
        var markerMatch = ScreenshotMarkerRegex().Match(responseText);
        if (markerMatch.Success)
        {
            foreach (var token in markerMatch.Groups[1].Value.Split(
                         '|',
                         StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                AddResolvedPath(files, token, contentRootPath);
            }
        }

        foreach (Match match in WindowsImagePathRegex().Matches(responseText))
            AddResolvedPath(files, match.Value, contentRootPath);

        return files;
    }

    public static IReadOnlyList<string> GetAutoCodexScreenshotFolders()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
            return [];

        var codexRoot = Path.Combine(userProfile, ".codex");
        if (!Directory.Exists(codexRoot))
            return [];

        var discovered = new List<string>();
        var mediaBrowserFolder = Path.Combine(codexRoot, "media", "browser");
        if (Directory.Exists(mediaBrowserFolder))
            discovered.Add(mediaBrowserFolder);

        try
        {
            discovered.AddRange(Directory.EnumerateDirectories(
                codexRoot,
                "Fix Screenshot",
                SearchOption.AllDirectories));
        }
        catch
        {
            // Keep any known accessible folders when recursive discovery is unavailable.
        }

        return discovered;
    }

    private static void AddResolvedPath(List<string> files, string candidate, string contentRootPath)
    {
        var normalized = candidate.Trim().Trim('"', '\'', '`', '*', '.', ',', ';', ')', ']', '}');
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        var resolved = ResolvePath(normalized, contentRootPath);
        if (!string.IsNullOrWhiteSpace(resolved))
            files.Add(resolved);
    }

    [GeneratedRegex(@"SCREENSHOT_PATHS?\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex ScreenshotMarkerRegex();

    [GeneratedRegex(@"(?:[A-Za-z]:\\|\\\\)[^\r\n]*?\.(?:png|jpg|jpeg|webp|gif|bmp)", RegexOptions.IgnoreCase)]
    private static partial Regex WindowsImagePathRegex();
}
