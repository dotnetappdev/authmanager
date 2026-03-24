namespace AuthManager.Core.Models;

/// <summary>
/// Runtime information about a configured OAuth provider.
/// </summary>
public sealed class OAuthProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsConfigured { get; set; }
    public Dictionary<string, string> Settings { get; set; } = [];
}

/// <summary>
/// DTO for updating OAuth provider settings from the UI.
/// </summary>
public sealed class UpdateOAuthProviderDto
{
    public string ProviderName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public Dictionary<string, string> Settings { get; set; } = [];
}
