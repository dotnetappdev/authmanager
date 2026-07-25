using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Manages multi-tenancy: isolated tenants that users are scoped to via the
/// <c>tenant_id</c> claim (configurable via <c>MultiTenancyOptions.TenantClaimType</c>).
/// </summary>
public interface ITenantService
{
    /// <summary>
    /// Lists all configured tenants with member counts. Includes a synthetic "root" tenant
    /// for users with no tenant claim when <c>AllowRootTenant</c> is enabled.
    /// </summary>
    Task<List<TenantDto>> GetTenantsAsync(CancellationToken ct = default);

    Task<TenantDto?> GetTenantAsync(string id, CancellationToken ct = default);

    Task<(bool Success, string[] Errors)> CreateTenantAsync(CreateTenantDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> UpdateTenantAsync(string id, UpdateTenantDto dto, CancellationToken ct = default);

    /// <summary>Deletes a tenant. Members are not deleted — they lose their tenant claim and fall back to the root tenant.</summary>
    Task<(bool Success, string[] Errors)> DeleteTenantAsync(string id, CancellationToken ct = default);

    Task<List<UserDto>> GetTenantMembersAsync(string tenantId, CancellationToken ct = default);

    /// <summary>Returns the tenant ID currently assigned to a user, or null if unassigned.</summary>
    Task<string?> GetUserTenantIdAsync(string userId, CancellationToken ct = default);

    /// <summary>Assigns a user to a tenant, replacing any previous tenant claim.</summary>
    Task<(bool Success, string[] Errors)> AssignUserToTenantAsync(string userId, string tenantId, CancellationToken ct = default);

    /// <summary>Removes a user's tenant claim, returning them to the root tenant.</summary>
    Task<(bool Success, string[] Errors)> RemoveUserFromTenantAsync(string userId, CancellationToken ct = default);
}
