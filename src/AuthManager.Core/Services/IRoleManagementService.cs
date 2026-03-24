using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Service for managing roles via ASP.NET Identity.
/// </summary>
public interface IRoleManagementService
{
    Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default);
    Task<RoleDto?> GetRoleByIdAsync(string roleId, CancellationToken ct = default);
    Task<RoleDto?> GetRoleByNameAsync(string roleName, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> CreateRoleAsync(CreateRoleDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> UpdateRoleAsync(UpdateRoleDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> DeleteRoleAsync(string roleId, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> AddClaimToRoleAsync(string roleId, ClaimDto claim, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> RemoveClaimFromRoleAsync(string roleId, ClaimDto claim, CancellationToken ct = default);
    Task<List<UserDto>> GetUsersInRoleAsync(string roleName, CancellationToken ct = default);
}
