namespace AuthManager.Core.Options;

/// <summary>
/// Multi-tenancy configuration — scope users and roles to isolated tenants.
/// Inspired by Firebase Auth multi-tenancy and Keycloak realms.
///
/// When enabled, every user and role has a <c>tenant_id</c> claim.
/// The AuthManager UI lets admins switch tenant context and manage users per tenant.
/// </summary>
public sealed class MultiTenancyOptions
{
    /// <summary>Enable multi-tenant isolation. Default: false.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Claim type that carries the tenant identifier. Default: "tenant_id".</summary>
    public string TenantClaimType { get; set; } = "tenant_id";

    /// <summary>
    /// When true, users without a tenant claim are treated as belonging to the
    /// root/default tenant. Default: true.
    /// </summary>
    public bool AllowRootTenant { get; set; } = true;

    /// <summary>
    /// List of pre-defined tenants. Additional tenants can be created at runtime
    /// via the AuthManager UI.
    /// </summary>
    public List<TenantDefinition> Tenants { get; set; } = [];
}

/// <summary>
/// A tenant definition used for bootstrap and display.
/// </summary>
public sealed class TenantDefinition
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Custom data bag for app-specific tenant metadata.</summary>
    public Dictionary<string, string> Metadata { get; set; } = [];
}
