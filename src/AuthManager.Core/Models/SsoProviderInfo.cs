namespace AuthManager.Core.Models;

/// <summary>
/// Runtime information about a configured SSO provider shown in the admin UI.
/// </summary>
public sealed class SsoProviderInfo
{
    /// <summary>Internal key, e.g. "entraid", "oidc:okta", "saml".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Human-readable name shown in the UI.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Provider category: EntraId, Oidc, Saml.</summary>
    public SsoProviderType Type { get; set; }

    /// <summary>Whether this provider is configured and active.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Whether the minimum required fields are filled in.</summary>
    public bool IsConfigured { get; set; }

    /// <summary>Masked settings to display (secrets are partially hidden).</summary>
    public Dictionary<string, string> Settings { get; set; } = [];
}

public enum SsoProviderType { EntraId, Oidc, Saml }

/// <summary>DTO for updating SSO provider settings from the UI.</summary>
public sealed class UpdateSsoProviderDto
{
    public string Key { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public Dictionary<string, string> Settings { get; set; } = [];
}
