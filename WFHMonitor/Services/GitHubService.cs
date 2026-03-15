using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public class GitHubSettings
{
    public string Token { get; set; } = string.Empty;
}

public record GitHubCommit(
    string Sha,
    string Message,
    string AuthorName,
    string AuthorEmail,
    DateTime Date,
    string Url
);

public record GitHubRepositoryInfo(
    string Name,
    string FullName,
    string HtmlUrl,
    string DefaultBranch,
    string? Description,
    string? Homepage
);

public class GitHubService : IGitHubService
{
    private readonly HttpClient _http;
    private readonly string _token;

    public GitHubService(HttpClient http, IOptions<GitHubSettings> settings)
    {
        _http = http;
        _token = settings.Value.Token;
    }

    public async Task<List<GitHubCommit>> GetCommitsAsync(string owner, string repo, string branch, int count = 10)
    {
        var json = await SendGitHubGetAsync(
            $"https://api.github.com/repos/{owner}/{repo}/commits?sha={branch}&per_page={count}");
        using var doc = JsonDocument.Parse(json);

        var commits = new List<GitHubCommit>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var sha = item.GetProperty("sha").GetString() ?? "";
            var commitNode = item.GetProperty("commit");
            var message = commitNode.GetProperty("message").GetString() ?? "";
            var authorNode = commitNode.GetProperty("author");
            var authorName = authorNode.GetProperty("name").GetString() ?? "";
            var authorEmail = authorNode.GetProperty("email").GetString() ?? "";
            var dateStr = authorNode.GetProperty("date").GetString() ?? "";
            DateTime.TryParse(dateStr, out var date);
            var url = item.GetProperty("html_url").GetString() ?? "";

            // Take only first line of commit message
            var firstLine = message.Split('\n')[0].Trim();

            commits.Add(new GitHubCommit(sha[..Math.Min(7, sha.Length)], firstLine, authorName, authorEmail, date, url));
        }
        return commits;
    }

    public async Task<List<string>> GetRepoTreeAsync(string owner, string repo, string branch)
    {
        var json = await SendGitHubGetAsync(
            $"https://api.github.com/repos/{owner}/{repo}/git/trees/{branch}?recursive=1");
        using var doc = JsonDocument.Parse(json);

        var paths = new List<string>();
        if (doc.RootElement.TryGetProperty("tree", out var tree))
        {
            foreach (var item in tree.EnumerateArray())
            {
                var path = item.GetProperty("path").GetString() ?? "";
                paths.Add(path);
            }
        }
        return paths;
    }

    public async Task<GitHubRepositoryInfo> GetRepositoryInfoAsync(string owner, string repo)
    {
        var json = await SendGitHubGetAsync(
            $"https://api.github.com/repos/{owner}/{repo}");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new GitHubRepositoryInfo(
            root.GetProperty("name").GetString() ?? repo,
            root.GetProperty("full_name").GetString() ?? $"{owner}/{repo}",
            root.GetProperty("html_url").GetString() ?? $"https://github.com/{owner}/{repo}",
            root.GetProperty("default_branch").GetString() ?? "main",
            root.GetProperty("description").GetString(),
            root.GetProperty("homepage").GetString()
        );
    }

    public async Task<string?> GetReadmeContentAsync(string owner, string repo)
    {
        var request = CreateRequest(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/readme");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        var response = await _http.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"GitHub API {(int)response.StatusCode}: {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("content", out var contentElement))
            return null;

        var encoded = contentElement.GetString();
        if (string.IsNullOrWhiteSpace(encoded))
            return null;

        var normalized = encoded.Replace("\n", string.Empty).Replace("\r", string.Empty);
        try
        {
            var bytes = Convert.FromBase64String(normalized);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    public async Task<Dictionary<string, long>> GetRepositoryLanguagesAsync(string owner, string repo)
    {
        var json = await SendGitHubGetAsync(
            $"https://api.github.com/repos/{owner}/{repo}/languages");

        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt64(out var bytes))
            {
                result[prop.Name] = bytes;
            }
        }

        return result;
    }

    private async Task<string> SendGitHubGetAsync(string url)
    {
        var request = CreateRequest(HttpMethod.Get, url);
        var response = await _http.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"GitHub API {(int)response.StatusCode}: {error}");
        }

        return await response.Content.ReadAsStringAsync();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.UserAgent.ParseAdd("WFHMonitor/1.0");

        if (!string.IsNullOrWhiteSpace(_token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        return request;
    }
}
