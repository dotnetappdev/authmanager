using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

internal sealed class OAuthProviderService : IOAuthProviderService
{
    private readonly AuthManagerOptions _options;
    private readonly ILogger<OAuthProviderService> _logger;

    public OAuthProviderService(IOptions<AuthManagerOptions> options, ILogger<OAuthProviderService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<List<OAuthProviderInfo>> GetProvidersAsync(CancellationToken ct = default)
    {
        var oauth = _options.OAuth;
        var providers = new List<OAuthProviderInfo>
        {
            new()
            {
                Name = "Google",
                DisplayName = "Google",
                IconClass = "icon-google",
                IsEnabled = oauth.Google.Enabled,
                IsConfigured = !string.IsNullOrEmpty(oauth.Google.ClientId),
                Settings = new Dictionary<string, string>
                {
                    ["ClientId"] = MaskSecret(oauth.Google.ClientId),
                    ["ClientSecret"] = MaskSecret(oauth.Google.ClientSecret)
                }
            },
            new()
            {
                Name = "Microsoft",
                DisplayName = "Microsoft",
                IconClass = "icon-microsoft",
                IsEnabled = oauth.Microsoft.Enabled,
                IsConfigured = !string.IsNullOrEmpty(oauth.Microsoft.ClientId),
                Settings = new Dictionary<string, string>
                {
                    ["ClientId"] = MaskSecret(oauth.Microsoft.ClientId),
                    ["ClientSecret"] = MaskSecret(oauth.Microsoft.ClientSecret),
                    ["TenantId"] = oauth.Microsoft.TenantId
                }
            },
            new()
            {
                Name = "Apple",
                DisplayName = "Apple",
                IconClass = "icon-apple",
                IsEnabled = oauth.Apple.Enabled,
                IsConfigured = !string.IsNullOrEmpty(oauth.Apple.ClientId),
                Settings = new Dictionary<string, string>
                {
                    ["ClientId"] = MaskSecret(oauth.Apple.ClientId),
                    ["TeamId"] = oauth.Apple.TeamId,
                    ["KeyId"] = oauth.Apple.KeyId
                }
            },
            new()
            {
                Name = "GitHub",
                DisplayName = "GitHub",
                IconClass = "icon-github",
                IsEnabled = oauth.GitHub.Enabled,
                IsConfigured = !string.IsNullOrEmpty(oauth.GitHub.ClientId),
                Settings = new Dictionary<string, string>
                {
                    ["ClientId"] = MaskSecret(oauth.GitHub.ClientId),
                    ["ClientSecret"] = MaskSecret(oauth.GitHub.ClientSecret)
                }
            }
        };

        foreach (var custom in oauth.CustomProviders)
        {
            providers.Add(new OAuthProviderInfo
            {
                Name = custom.Name,
                DisplayName = custom.DisplayName,
                IconClass = "icon-custom",
                IsEnabled = true,
                IsConfigured = !string.IsNullOrEmpty(custom.ClientId),
                Settings = new Dictionary<string, string>
                {
                    ["ClientId"] = MaskSecret(custom.ClientId),
                    ["AuthorizationEndpoint"] = custom.AuthorizationEndpoint,
                    ["TokenEndpoint"] = custom.TokenEndpoint
                }
            });
        }

        return Task.FromResult(providers);
    }

    public Task<OAuthProviderInfo?> GetProviderAsync(string providerName, CancellationToken ct = default)
        => GetProvidersAsync(ct).ContinueWith(t =>
            t.Result.FirstOrDefault(p => string.Equals(p.Name, providerName, StringComparison.OrdinalIgnoreCase)));

    public Task<(bool Success, string[] Errors)> UpdateProviderAsync(UpdateOAuthProviderDto dto, CancellationToken ct = default)
    {
        // In a real implementation, persist changes to configuration store
        _logger.LogInformation("OAuth provider {ProviderName} configuration updated.", dto.ProviderName);
        return Task.FromResult<(bool, string[])>((true, []));
    }

    private static string MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= 8) return new string('*', value.Length);
        return value[..4] + new string('*', value.Length - 8) + value[^4..];
    }
}
