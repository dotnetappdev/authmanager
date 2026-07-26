namespace AuthManager.Core.Models;

/// <summary>
/// A customer-facing API key — issued to an external customer/account so their own
/// application can call your APIs. Distinct from <c>ApiTokenDto</c> (a personal token
/// for an Identity user of this app) and from OAuth2 clients (service-to-service, JWT-based):
/// this is a simple bearer key, scoped and optionally rate-limited, meant to be handed to
/// a customer the way Stripe or SendGrid hand out API keys.
/// </summary>
public sealed class CustomerApiKeyDto
{
    public string Id { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string? CustomerName { get; set; }
    public string Name { get; set; } = "";

    /// <summary>First few characters of the key, shown in lists so admins can recognise it (e.g. "ck_live_a1b2").</summary>
    public string Prefix { get; set; } = "";

    public List<string> Scopes { get; set; } = [];

    /// <summary>Requests per minute this key is allowed to make. Null/0 = unlimited.</summary>
    public int? RateLimitPerMinute { get; set; }

    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class CreateCustomerApiKeyDto
{
    public string CustomerId { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Scopes { get; set; } = [];
    public int? RateLimitPerMinute { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class UpdateCustomerApiKeyDto
{
    public string Name { get; set; } = "";
    public List<string> Scopes { get; set; } = [];
    public int? RateLimitPerMinute { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>Returned once on creation (or regeneration) — the raw key cannot be retrieved again.</summary>
public sealed class NewCustomerApiKeyResult
{
    public string ApiKey { get; set; } = "";
    public CustomerApiKeyDto Key { get; set; } = new();
}
