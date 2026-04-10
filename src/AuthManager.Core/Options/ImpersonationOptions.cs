namespace AuthManager.Core.Options;

/// <summary>
/// Admin impersonation — lets a SuperAdmin sign in as any user for debugging.
/// Equivalent to Keycloak's "Impersonate" button on the user detail page.
///
/// When an admin impersonates a user, the original admin identity is stored in a
/// claim (<c>impersonated_by</c>) so the app can differentiate.
///
/// ⚠️  All impersonation actions are written to the audit log.
/// </summary>
public sealed class ImpersonationOptions
{
    /// <summary>Enable admin impersonation. Default: false.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Roles that are allowed to impersonate other users.
    /// Defaults to <see cref="AuthManagerOptions.SuperAdminRole"/>.
    /// </summary>
    public string[] AllowedRoles { get; set; } = [];

    /// <summary>Claim type used to record the original admin's ID. Default: "impersonated_by".</summary>
    public string ImpersonatedByClaimType { get; set; } = "impersonated_by";

    /// <summary>
    /// Prevent a SuperAdmin from impersonating another SuperAdmin.
    /// Recommended to avoid privilege confusion. Default: true.
    /// </summary>
    public bool BlockImpersonatingAdmins { get; set; } = true;
}
