namespace WFHMonitor.Services;

public class GitHubOAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int StateLifetimeMinutes { get; set; }
    public bool ForceAccountPicker { get; set; }
    public string AuthorizeUrl { get; set; } = string.Empty;
    public string AccessTokenUrl { get; set; } = string.Empty;
    public string UserApiUrl { get; set; } = string.Empty;
}
