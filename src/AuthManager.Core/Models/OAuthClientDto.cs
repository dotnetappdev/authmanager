namespace AuthManager.Core.Models;

/// <summary>
/// A registered OAuth2 client application (Keycloak calls these "Clients") — a service or app
/// that authenticates as itself, not as a user, via the client-credentials grant.
/// </summary>
public sealed class OAuthClientDto
{
    public string Id { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;
    public List<string> AllowedScopes { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}

public sealed class CreateOAuthClientDto
{
    /// <summary>Public identifier — stable, unique, safe to log (e.g. "billing-service").</summary>
    public string ClientId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<string> AllowedScopes { get; set; } = [];
}

public sealed class UpdateOAuthClientDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;
    public List<string> AllowedScopes { get; set; } = [];
}

/// <summary>Returned once on creation (or secret regeneration) — the raw secret cannot be retrieved again.</summary>
public sealed class NewOAuthClientResult
{
    public string ClientSecret { get; set; } = "";
    public OAuthClientDto Client { get; set; } = new();
}
