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
        var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{owner}/{repo}/commits?sha={branch}&per_page={count}");

        request.Headers.UserAgent.ParseAdd("WFHMonitor/1.0");
        if (!string.IsNullOrWhiteSpace(_token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"GitHub API {(int)response.StatusCode}: {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
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
}
