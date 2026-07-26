using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Manages SSO provider configuration (Entra ID, generic OIDC, SAML 2.0) shown in the admin
/// UI. Defaults come from <see cref="AuthManagerOptions.Sso"/> (set in <c>AddAuthManager()</c>);
/// runtime edits are persisted to AuthManager's internal database so they survive restarts and
/// don't require redeploying the app to change a client secret or add an OIDC provider.
///
/// AuthManager configures SSO providers — it does not itself register the authentication
/// middleware. Wire <c>.AddOpenIdConnect()</c> (Entra ID and generic OIDC both use it) or a
/// SAML service-provider library in your own <c>Program.cs</c>, reading these values the same
/// way you'd read any other configuration. See the README's SSO section for a worked example.
/// </summary>
internal sealed class SsoService : ISsoService
{
    private const string EntraIdKey = "Sso:EntraId";
    private const string SamlKey    = "Sso:Saml";
    private const string OidcKey    = "Sso:OidcProviders";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly IOptionsMonitor<AuthManagerOptions> _monitor;
    private readonly ILogger<SsoService> _logger;

    public SsoService(
        IDbContextFactory<AuthManagerDbContext> factory,
        IOptionsMonitor<AuthManagerOptions> monitor,
        ILogger<SsoService> logger)
    {
        _factory = factory;
        _monitor = monitor;
        _logger  = logger;
    }

    public async Task<List<SsoProviderInfo>> GetProvidersAsync(CancellationToken ct = default)
    {
        var entra = await GetEntraIdAsync(ct);
        var saml  = await GetSamlAsync(ct);
        var oidcProviders = await GetOidcProvidersAsync(ct);

        var list = new List<SsoProviderInfo>
        {
            new()
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
                    ["GroupMappings"]     = entra.GroupToRoleMapping.Count == 0 ? "(none)" : $"{entra.GroupToRoleMapping.Count} mapped",
                }
            }
        };

        foreach (var oidc in oidcProviders)
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
                ["NameIdAttributeName"]           = string.IsNullOrEmpty(saml.NameIdAttributeName) ? "(default: NameID)" : saml.NameIdAttributeName,
            }
        });

        return list;
    }

    public async Task<SsoProviderInfo?> GetProviderAsync(string key, CancellationToken ct = default)
    {
        var all = await GetProvidersAsync(ct);
        return all.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<(bool Success, string[] Errors)> UpdateProviderAsync(
        UpdateSsoProviderDto dto, CancellationToken ct = default)
    {
        if (string.Equals(dto.Key, "entraid", StringComparison.OrdinalIgnoreCase))
        {
            var entra = await GetEntraIdAsync(ct);
            entra.Enabled = dto.Enabled;
            ApplyIfPresent(dto.Settings, "TenantId", v => entra.TenantId = v);
            ApplyIfPresent(dto.Settings, "ClientId", v => entra.ClientId = v);
            ApplyIfPresentNonEmpty(dto.Settings, "ClientSecret", v => entra.ClientSecret = v);
            ApplyIfPresent(dto.Settings, "Authority", v => entra.Authority = v);
            ApplyIfPresent(dto.Settings, "CallbackPath", v => entra.CallbackPath = v);
            ApplyIfPresent(dto.Settings, "AdditionalScopes", v => entra.AdditionalScopes = v);
            ApplyIfPresent(dto.Settings, "EnableGroupToRoleSync", v => entra.EnableGroupToRoleSync = v == "true");
            ApplyIfPresent(dto.Settings, "GroupToRoleMappingJson", v =>
                entra.GroupToRoleMapping = JsonSerializer.Deserialize<Dictionary<string, string>>(v, _json) ?? []);

            await UpsertAsync(EntraIdKey, entra, ct);
            _logger.LogInformation("[DotNetAuthManager] Entra ID SSO settings updated (Enabled={Enabled}).", entra.Enabled);
            return (true, []);
        }

        if (string.Equals(dto.Key, "saml", StringComparison.OrdinalIgnoreCase))
        {
            var saml = await GetSamlAsync(ct);
            saml.Enabled = dto.Enabled;
            ApplyIfPresent(dto.Settings, "ServiceProviderEntityId", v => saml.ServiceProviderEntityId = v);
            ApplyIfPresent(dto.Settings, "IdentityProviderSsoUrl", v => saml.IdentityProviderSsoUrl = v);
            ApplyIfPresent(dto.Settings, "AssertionConsumerServicePath", v => saml.AssertionConsumerServicePath = v);
            ApplyIfPresent(dto.Settings, "EmailAttributeName", v => saml.EmailAttributeName = v);
            ApplyIfPresent(dto.Settings, "NameIdAttributeName", v => saml.NameIdAttributeName = v);
            // Only overwrite the certificate if a new one was uploaded — an empty value means
            // "leave the existing certificate alone", since the UI never re-displays it.
            ApplyIfPresentNonEmpty(dto.Settings, "IdentityProviderCertificate", v => saml.IdentityProviderCertificate = v);

            await UpsertAsync(SamlKey, saml, ct);
            _logger.LogInformation("[DotNetAuthManager] SAML SSO settings updated (Enabled={Enabled}).", saml.Enabled);
            return (true, []);
        }

        if (dto.Key.StartsWith("oidc:", StringComparison.OrdinalIgnoreCase))
        {
            var name = dto.Key["oidc:".Length..];
            var providers = await GetOidcProvidersAsync(ct);
            var existing = providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing is null) return (false, [$"OIDC provider '{name}' not found."]);

            existing.Enabled = dto.Enabled;
            ApplyIfPresent(dto.Settings, "Authority", v => existing.Authority = v);
            ApplyIfPresent(dto.Settings, "ClientId", v => existing.ClientId = v);
            ApplyIfPresentNonEmpty(dto.Settings, "ClientSecret", v => existing.ClientSecret = v);
            ApplyIfPresent(dto.Settings, "CallbackPath", v => existing.CallbackPath = v);
            ApplyIfPresent(dto.Settings, "AdditionalScopes", v => existing.AdditionalScopes = v);
            ApplyIfPresent(dto.Settings, "UserIdClaim", v => existing.UserIdClaim = v);

            await UpsertAsync(OidcKey, providers, ct);
            _logger.LogInformation("[DotNetAuthManager] OIDC provider '{Name}' settings updated (Enabled={Enabled}).", name, existing.Enabled);
            return (true, []);
        }

        return (false, [$"Unknown SSO provider key '{dto.Key}'."]);
    }

    public async Task<(bool Success, string[] Errors)> CreateOidcProviderAsync(
        CreateOidcProviderDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, ["Name is required."]);
        if (string.IsNullOrWhiteSpace(dto.DisplayName))
            return (false, ["Display name is required."]);

        var providers = await GetOidcProvidersAsync(ct);
        if (providers.Any(p => string.Equals(p.Name, dto.Name, StringComparison.OrdinalIgnoreCase)))
            return (false, [$"A provider named '{dto.Name}' already exists."]);

        providers.Add(new OidcSsoProviderOptions
        {
            Name             = dto.Name.Trim(),
            DisplayName      = dto.DisplayName.Trim(),
            Enabled          = true,
            Authority        = dto.Authority,
            ClientId         = dto.ClientId,
            ClientSecret     = dto.ClientSecret,
            CallbackPath     = string.IsNullOrWhiteSpace(dto.CallbackPath) ? $"/signin-oidc-{dto.Name.Trim().ToLowerInvariant()}" : dto.CallbackPath,
            AdditionalScopes = dto.AdditionalScopes,
            UserIdClaim      = string.IsNullOrWhiteSpace(dto.UserIdClaim) ? "sub" : dto.UserIdClaim
        });

        await UpsertAsync(OidcKey, providers, ct);
        _logger.LogInformation("[DotNetAuthManager] OIDC provider '{Name}' registered.", dto.Name);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> DeleteOidcProviderAsync(string name, CancellationToken ct = default)
    {
        var providers = await GetOidcProvidersAsync(ct);
        var existing = providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is null) return (false, [$"OIDC provider '{name}' not found."]);

        providers.Remove(existing);
        await UpsertAsync(OidcKey, providers, ct);
        _logger.LogInformation("[DotNetAuthManager] OIDC provider '{Name}' removed.", name);
        return (true, []);
    }

    // ── Persistence helpers ──────────────────────────────────────────────────

    private async Task<EntraIdSsoOptions> GetEntraIdAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Settings.FindAsync([EntraIdKey], ct);
        return row is null
            ? Clone(_monitor.CurrentValue.Sso.EntraId)
            : JsonSerializer.Deserialize<EntraIdSsoOptions>(row.ValueJson, _json) ?? Clone(_monitor.CurrentValue.Sso.EntraId);
    }

    private async Task<SamlSsoOptions> GetSamlAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Settings.FindAsync([SamlKey], ct);
        return row is null
            ? Clone(_monitor.CurrentValue.Sso.Saml)
            : JsonSerializer.Deserialize<SamlSsoOptions>(row.ValueJson, _json) ?? Clone(_monitor.CurrentValue.Sso.Saml);
    }

    private async Task<List<OidcSsoProviderOptions>> GetOidcProvidersAsync(CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Settings.FindAsync([OidcKey], ct);
        if (row is not null)
        {
            var stored = JsonSerializer.Deserialize<List<OidcSsoProviderOptions>>(row.ValueJson, _json);
            if (stored is not null) return stored;
        }
        // Seed from code-configured defaults on first access.
        return _monitor.CurrentValue.Sso.OidcProviders
            .Select(p => new OidcSsoProviderOptions
            {
                Name = p.Name, DisplayName = p.DisplayName, Enabled = p.Enabled, Authority = p.Authority,
                ClientId = p.ClientId, ClientSecret = p.ClientSecret, CallbackPath = p.CallbackPath,
                AdditionalScopes = p.AdditionalScopes, UserIdClaim = p.UserIdClaim
            })
            .ToList();
    }

    private async Task UpsertAsync<T>(string key, T value, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Settings.FindAsync([key], ct);
        var json = JsonSerializer.Serialize(value, _json);

        if (row is null)
            db.Settings.Add(new AuthManagerSettingRecord { Key = key, ValueJson = json });
        else
        {
            row.ValueJson = json;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private static void ApplyIfPresent(Dictionary<string, string> settings, string key, Action<string> apply)
    {
        if (settings.TryGetValue(key, out var v)) apply(v);
    }

    private static void ApplyIfPresentNonEmpty(Dictionary<string, string> settings, string key, Action<string> apply)
    {
        if (settings.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)) apply(v);
    }

    private static string MaskSecret(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Length <= 8) return new string('*', value.Length);
        return value[..4] + new string('*', value.Length - 8) + value[^4..];
    }

    private static EntraIdSsoOptions Clone(EntraIdSsoOptions src) => new()
    {
        Enabled = src.Enabled,
        TenantId = src.TenantId,
        ClientId = src.ClientId,
        ClientSecret = src.ClientSecret,
        Authority = src.Authority,
        AdditionalScopes = src.AdditionalScopes,
        GroupToRoleMapping = new Dictionary<string, string>(src.GroupToRoleMapping),
        EnableGroupToRoleSync = src.EnableGroupToRoleSync,
        CallbackPath = src.CallbackPath
    };

    private static SamlSsoOptions Clone(SamlSsoOptions src) => new()
    {
        Enabled = src.Enabled,
        ServiceProviderEntityId = src.ServiceProviderEntityId,
        IdentityProviderSsoUrl = src.IdentityProviderSsoUrl,
        IdentityProviderCertificate = src.IdentityProviderCertificate,
        AssertionConsumerServicePath = src.AssertionConsumerServicePath,
        EmailAttributeName = src.EmailAttributeName,
        NameIdAttributeName = src.NameIdAttributeName
    };
}
