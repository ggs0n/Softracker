namespace WFHMonitor.Services;

public class GitHubOAuthSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Scope { get; set; } = "repo read:user";
    public int StateLifetimeMinutes { get; set; } = 15;
    public bool ForceAccountPicker { get; set; } = true;
}
