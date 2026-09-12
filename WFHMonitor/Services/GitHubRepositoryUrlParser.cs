using System.Text.RegularExpressions;

namespace WFHMonitor.Services;

public static partial class GitHubRepositoryUrlParser
{
    public static bool TryParse(
        string url,
        out string owner,
        out string repository,
        out string? branch)
    {
        owner = string.Empty;
        repository = string.Empty;
        branch = null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Trim('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return false;

        owner = segments[0];
        repository = GitSuffixRegex().Replace(segments[1], string.Empty);
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository))
            return false;

        if (segments.Length >= 4 && string.Equals(segments[2], "tree", StringComparison.OrdinalIgnoreCase))
            branch = string.Join('/', segments.Skip(3));

        return true;
    }

    [GeneratedRegex(@"\.git$", RegexOptions.IgnoreCase)]
    private static partial Regex GitSuffixRegex();
}
