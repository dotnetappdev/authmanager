namespace AuthManager.Core.Models;

/// <summary>An isolated tenant that users can be scoped to via the <c>tenant_id</c> claim.</summary>
public sealed class TenantDto
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int MemberCount { get; set; }

    /// <summary>
    /// True for the synthetic "root" tenant representing users with no tenant claim.
    /// Only present when <c>MultiTenancyOptions.AllowRootTenant</c> is enabled. Cannot be edited or deleted.
    /// </summary>
    public bool IsRootTenant { get; set; }

    public Dictionary<string, string> Metadata { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CreateTenantDto
{
    /// <summary>Stable identifier stored in the tenant claim value. Lowercase, no spaces recommended.</summary>
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = [];
}

public sealed class UpdateTenantDto
{
    public string DisplayName { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = [];
}
