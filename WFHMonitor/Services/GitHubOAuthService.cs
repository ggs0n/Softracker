using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using WFHMonitor.Models;
using WFHMonitor.Services.Interfaces;

namespace WFHMonitor.Services;

public class GitHubOAuthService : IGitHubOAuthService
{
    private const string GitHubAuthorizeUrl = "https://github.com/login/oauth/authorize";
    private const string GitHubAccessTokenUrl = "https://github.com/login/oauth/access_token";
    private const string GitHubUserApiUrl = "https://api.github.com/user";
    private const string LoginProvider = "GitHubOAuth";
    private const string AccessTokenName = "AccessToken";
    private const string ScopeTokenName = "Scope";
    private const string LoginTokenName = "Login";
    private const string ConnectedAtTokenName = "ConnectedAtUtc";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly GitHubOAuthSettings _settings;
    private readonly IDataProtector _stateProtector;

    public GitHubOAuthService(
        IHttpClientFactory httpClientFactory,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        IOptions<GitHubOAuthSettings> settings,
        IDataProtectionProvider dataProtectionProvider)
    {
        _httpClientFactory = httpClientFactory;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        _settings = settings.Value;
        _stateProtector = dataProtectionProvider.CreateProtector("WFHMonitor.GitHubOAuth.State.v1");
    }

    public bool IsOAuthConfigured()
    {
        return !string.IsNullOrWhiteSpace(_settings.ClientId)
            && !string.IsNullOrWhiteSpace(_settings.ClientSecret)
            && !string.IsNullOrWhiteSpace(_settings.RedirectUri);
    }

    public async Task<bool> IsConnectedAsync(string userId)
    {
        var token = await GetAccessTokenForUserAsync(userId);
        return !string.IsNullOrWhiteSpace(token);
    }

    public async Task<string?> GetCurrentUserAccessTokenAsync()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return await GetAccessTokenForUserAsync(userId);
    }

    public Task<string> BuildAuthorizeUrlAsync(string userId, string? returnUrl)
    {
        if (!IsOAuthConfigured())
            throw new InvalidOperationException("GitHub OAuth is not configured.");

        var normalizedReturnUrl = NormalizeReturnUrl(returnUrl);
        var payload = new GitHubOAuthStatePayload
        {
            UserId = userId,
            ReturnUrl = normalizedReturnUrl,
            IssuedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Nonce = Guid.NewGuid().ToString("N")
        };

        var serializedPayload = JsonSerializer.Serialize(payload);
        var protectedState = _stateProtector.Protect(serializedPayload);

        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _settings.ClientId,
            ["redirect_uri"] = _settings.RedirectUri,
            ["scope"] = string.IsNullOrWhiteSpace(_settings.Scope) ? "repo read:user" : _settings.Scope,
            ["state"] = protectedState
        };

        if (_settings.ForceAccountPicker)
            query["prompt"] = "select_account";

        var url = QueryHelpers.AddQueryString(GitHubAuthorizeUrl, query);
        return Task.FromResult(url);
    }

    public async Task<GitHubOAuthCallbackResult> CompleteAuthorizationAsync(
        string code,
        string state,
        string currentUserId)
    {
        if (!IsOAuthConfigured())
        {
            return new GitHubOAuthCallbackResult
            {
                Succeeded = false,
                ErrorMessage = "GitHub OAuth is not configured by admin."
            };
        }

        GitHubOAuthStatePayload payload;
        try
        {
            var json = _stateProtector.Unprotect(state);
            payload = JsonSerializer.Deserialize<GitHubOAuthStatePayload>(json) ?? new GitHubOAuthStatePayload();
        }
        catch
        {
            return new GitHubOAuthCallbackResult
            {
                Succeeded = false,
                ErrorMessage = "Invalid GitHub authorization state."
            };
        }

        if (string.IsNullOrWhiteSpace(payload.UserId) || payload.UserId != currentUserId)
        {
            return new GitHubOAuthCallbackResult
            {
                Succeeded = false,
                ReturnUrl = NormalizeReturnUrl(payload.ReturnUrl),
                ErrorMessage = "GitHub authorization state does not match the current user."
            };
        }

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(payload.IssuedAtUnix);
        if (DateTimeOffset.UtcNow - issuedAt > TimeSpan.FromMinutes(Math.Max(1, _settings.StateLifetimeMinutes)))
        {
            return new GitHubOAuthCallbackResult
            {
                Succeeded = false,
                ReturnUrl = NormalizeReturnUrl(payload.ReturnUrl),
                ErrorMessage = "GitHub authorization state expired. Please connect again."
            };
        }

        var user = await _userManager.FindByIdAsync(currentUserId);
        if (user is null)
        {
            return new GitHubOAuthCallbackResult
            {
                Succeeded = false,
                ReturnUrl = NormalizeReturnUrl(payload.ReturnUrl),
                ErrorMessage = "Unable to find current user."
            };
        }

        var http = _httpClientFactory.CreateClient();
        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, GitHubAccessTokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _settings.ClientId,
                ["client_secret"] = _settings.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = _settings.RedirectUri
            })
        };
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        tokenRequest.Headers.UserAgent.ParseAdd("WFHMonitor/1.0");

        var tokenResponse = await http.SendAsync(tokenRequest);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
        {
            return new GitHubOAuthCallbackResult
            {
                Succeeded = false,
                ReturnUrl = NormalizeReturnUrl(payload.ReturnUrl),
                ErrorMessage = "GitHub token exchange failed."
            };
        }

        using var tokenDoc = JsonDocument.Parse(tokenJson);
        var root = tokenDoc.RootElement;

        var accessToken = root.TryGetProperty("access_token", out var tokenEl)
            ? tokenEl.GetString()
            : null;
        var scope = root.TryGetProperty("scope", out var scopeEl)
            ? scopeEl.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            var providerError = root.TryGetProperty("error_description", out var errEl)
                ? errEl.GetString()
                : "No access token was returned by GitHub.";

            return new GitHubOAuthCallbackResult
            {
                Succeeded = false,
                ReturnUrl = NormalizeReturnUrl(payload.ReturnUrl),
                ErrorMessage = providerError
            };
        }

        string? githubLogin = null;
        try
        {
            var userRequest = new HttpRequestMessage(HttpMethod.Get, GitHubUserApiUrl);
            userRequest.Headers.UserAgent.ParseAdd("WFHMonitor/1.0");
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var userResponse = await http.SendAsync(userRequest);
            if (userResponse.IsSuccessStatusCode)
            {
                var userJson = await userResponse.Content.ReadAsStringAsync();
                using var userDoc = JsonDocument.Parse(userJson);
                githubLogin = userDoc.RootElement.TryGetProperty("login", out var loginEl)
                    ? loginEl.GetString()
                    : null;
            }
        }
        catch
        {
            // Non-blocking: token is enough to continue.
        }

        await _userManager.SetAuthenticationTokenAsync(user, LoginProvider, AccessTokenName, accessToken);
        await SetOrClearTokenAsync(user, ScopeTokenName, scope);
        await SetOrClearTokenAsync(user, LoginTokenName, githubLogin);
        await _userManager.SetAuthenticationTokenAsync(user, LoginProvider, ConnectedAtTokenName, DateTime.UtcNow.ToString("O"));

        return new GitHubOAuthCallbackResult
        {
            Succeeded = true,
            ReturnUrl = NormalizeReturnUrl(payload.ReturnUrl)
        };
    }

    public async Task DisconnectAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return;

        await _userManager.RemoveAuthenticationTokenAsync(user, LoginProvider, AccessTokenName);
        await _userManager.RemoveAuthenticationTokenAsync(user, LoginProvider, ScopeTokenName);
        await _userManager.RemoveAuthenticationTokenAsync(user, LoginProvider, LoginTokenName);
        await _userManager.RemoveAuthenticationTokenAsync(user, LoginProvider, ConnectedAtTokenName);
    }

    private async Task<string?> GetAccessTokenForUserAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return null;

        return await _userManager.GetAuthenticationTokenAsync(user, LoginProvider, AccessTokenName);
    }

    private async Task SetOrClearTokenAsync(ApplicationUser user, string tokenName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            await _userManager.RemoveAuthenticationTokenAsync(user, LoginProvider, tokenName);
            return;
        }

        await _userManager.SetAuthenticationTokenAsync(user, LoginProvider, tokenName, value.Trim());
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/ChangeRequest/Create";

        var normalized = returnUrl.Trim();
        if (normalized.StartsWith("/", StringComparison.Ordinal)
            && !normalized.StartsWith("//", StringComparison.Ordinal)
            && !normalized.StartsWith("/\\", StringComparison.Ordinal))
        {
            return normalized;
        }

        return "/ChangeRequest/Create";
    }

    private class GitHubOAuthStatePayload
    {
        public string UserId { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = "/ChangeRequest/Create";
        public long IssuedAtUnix { get; set; }
        public string Nonce { get; set; } = string.Empty;
    }
}
