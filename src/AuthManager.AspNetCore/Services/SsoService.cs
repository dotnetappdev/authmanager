using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Returns SSO provider configuration (Entra ID, OIDC, SAML 2.0) for the admin UI.
/// Settings are read from <see cref="AuthManagerOptions.Sso"/> and can be persisted
/// via <see cref="UpdateProviderAsync"/> (hook up to your config store as needed).
/// </summary>
internal sealed class SsoService : ISsoService
{
    private readonly AuthManagerOptions _options;
    private readonly ILogger<SsoService> _logger;

    public SsoService(IOptions<AuthManagerOptions> options, ILogger<SsoService> logger)
    {
        _options = options.Value;
        _logger  = logger;
    }

    public Task<List<SsoProviderInfo>> GetProvidersAsync(CancellationToken ct = default)
    {
        var list = new List<SsoProviderInfo>();

        // ── Entra ID ──────────────────────────────────────────────────────────
        var entra = _options.Sso.EntraId;
        list.Add(new SsoProviderInfo
        {
            Key          = "entraid",
            DisplayName  = "Microsoft Entra ID (Azure AD)",
            Type         = SsoProviderType.EntraId,
            IsEnabled    = entra.Enabled,
            IsConfigured = !string.IsNullOrEmpty(entra.ClientId) && !string.IsNullOrEmpty(entra.TenantId),
            Settings     = new Dictionary<string, string>
            {
                ["TenantId"]          = entra.TenantId,
                ["ClientId"]          = MaskSecret(entra.ClientId),
                ["ClientSecret"]      = MaskSecret(entra.ClientSecret),
                ["Authority"]         = entra.Authority.Replace("{tenantId}", entra.TenantId),
                ["CallbackPath"]      = entra.CallbackPath,
                ["AdditionalScopes"]  = entra.AdditionalScopes,
                ["GroupToRoleSync"]   = entra.EnableGroupToRoleSync ? "Enabled" : "Disabled",
            }
        });

        // ── Generic OIDC providers ────────────────────────────────────────────
        foreach (var oidc in _options.Sso.OidcProviders)
        {
            list.Add(new SsoProviderInfo
            {
                Key          = $"oidc:{oidc.Name.ToLowerInvariant()}",
                DisplayName  = oidc.DisplayName,
                Type         = SsoProviderType.Oidc,
                IsEnabled    = oidc.Enabled,
                IsConfigured = !string.IsNullOrEmpty(oidc.ClientId) && !string.IsNullOrEmpty(oidc.Authority),
                Settings     = new Dictionary<string, string>
                {
                    ["Authority"]        = oidc.Authority,
                    ["ClientId"]         = MaskSecret(oidc.ClientId),
                    ["ClientSecret"]     = MaskSecret(oidc.ClientSecret),
                    ["CallbackPath"]     = oidc.CallbackPath,
                    ["AdditionalScopes"] = oidc.AdditionalScopes,
                    ["UserIdClaim"]      = oidc.UserIdClaim,
                }
            });
        }

        // ── SAML 2.0 ─────────────────────────────────────────────────────────
        var saml = _options.Sso.Saml;
        list.Add(new SsoProviderInfo
        {
            Key          = "saml",
            DisplayName  = "SAML 2.0",
            Type         = SsoProviderType.Saml,
            IsEnabled    = saml.Enabled,
            IsConfigured = !string.IsNullOrEmpty(saml.IdentityProviderSsoUrl)
                        && !string.IsNullOrEmpty(saml.ServiceProviderEntityId),
            Settings     = new Dictionary<string, string>
            {
                ["ServiceProviderEntityId"]      = saml.ServiceProviderEntityId,
                ["IdentityProviderSsoUrl"]        = saml.IdentityProviderSsoUrl,
                ["AssertionConsumerServicePath"]  = saml.AssertionConsumerServicePath,
                ["IdP Certificate"]               = string.IsNullOrEmpty(saml.IdentityProviderCertificate)
                                                        ? "(not configured)"
                                                        : "✓ configured",
                ["EmailAttributeName"]            = saml.EmailAttributeName,
            }
        });

        return Task.FromResult(list);
    }

    public async Task<SsoProviderInfo?> GetProviderAsync(string key, CancellationToken ct = default)
    {
        var all = await GetProvidersAsync(ct);
        return all.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public Task<(bool Success, string[] Errors)> UpdateProviderAsync(
        UpdateSsoProviderDto dto, CancellationToken ct = default)
    {
        // In a production implementation, persist changes to the settings store or
        // the IConfiguration reload pipeline. For now, log the update.
        _logger.LogInformation(
            "[DotNetAuthManager] SSO provider '{Key}' settings updated (Enabled={Enabled}).",
            dto.Key, dto.Enabled);

        return Task.FromResult<(bool, string[])>((true, []));
    }

    private static string MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= 8) return new string('*', value.Length);
        return value[..4] + new string('*', value.Length - 8) + value[^4..];
    }
}
