using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthManager.AspNetCore.Services;

internal sealed class RoleManagementService<TRole> : IRoleManagementService
    where TRole : IdentityRole, new()
{
    private readonly RoleManager<TRole> _roleManager;
    private readonly ILogger<RoleManagementService<TRole>> _logger;

    public RoleManagementService(RoleManager<TRole> roleManager, ILogger<RoleManagementService<TRole>> logger)
    {
        _roleManager = roleManager;
        _logger = logger;
    }

    public async Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default)
    {
        var roles = await _roleManager.Roles.ToListAsync(ct);
        var dtos = new List<RoleDto>();

        foreach (var role in roles)
        {
            var claims = await _roleManager.GetClaimsAsync(role);
            dtos.Add(new RoleDto
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                Claims = claims.Select(c => new ClaimDto(c.Type, c.Value)).ToList()
            });
        }

        return dtos;
    }

    public async Task<RoleDto?> GetRoleByIdAsync(string roleId, CancellationToken ct = default)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null) return null;

        var claims = await _roleManager.GetClaimsAsync(role);
        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name ?? string.Empty,
            Claims = claims.Select(c => new ClaimDto(c.Type, c.Value)).ToList()
        };
    }

    public async Task<RoleDto?> GetRoleByNameAsync(string roleName, CancellationToken ct = default)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role is null) return null;

        var claims = await _roleManager.GetClaimsAsync(role);
        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name ?? string.Empty,
            Claims = claims.Select(c => new ClaimDto(c.Type, c.Value)).ToList()
        };
    }

    public async Task<(bool Success, string[] Errors)> CreateRoleAsync(CreateRoleDto dto, CancellationToken ct = default)
    {
        var role = new TRole { Name = dto.Name };
        var result = await _roleManager.CreateAsync(role);

        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        foreach (var claim in dto.Claims)
        {
            await _roleManager.AddClaimAsync(role, new System.Security.Claims.Claim(claim.Type, claim.Value));
        }

        _logger.LogInformation("Role {RoleName} created.", dto.Name);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> UpdateRoleAsync(UpdateRoleDto dto, CancellationToken ct = default)
    {
        var role = await _roleManager.FindByIdAsync(dto.Id);
        if (role is null)
            return (false, [$"Role {dto.Id} not found."]);

        role.Name = dto.Name;
        var result = await _roleManager.UpdateAsync(role);

        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        _logger.LogInformation("Role {RoleId} updated.", dto.Id);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> DeleteRoleAsync(string roleId, CancellationToken ct = default)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
            return (false, [$"Role {roleId} not found."]);

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
            return (false, result.Errors.Select(e => e.Description).ToArray());

        _logger.LogInformation("Role {RoleId} deleted.", roleId);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> AddClaimToRoleAsync(string roleId, ClaimDto claim, CancellationToken ct = default)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
            return (false, [$"Role {roleId} not found."]);

        var result = await _roleManager.AddClaimAsync(role, new System.Security.Claims.Claim(claim.Type, claim.Value));
        return result.Succeeded
            ? (true, [])
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, string[] Errors)> RemoveClaimFromRoleAsync(string roleId, ClaimDto claim, CancellationToken ct = default)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role is null)
            return (false, [$"Role {roleId} not found."]);

        var result = await _roleManager.RemoveClaimAsync(role, new System.Security.Claims.Claim(claim.Type, claim.Value));
        return result.Succeeded
            ? (true, [])
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public Task<List<UserDto>> GetUsersInRoleAsync(string roleName, CancellationToken ct = default)
    {
        // Requires UserManager - return empty for now
        return Task.FromResult(new List<UserDto>());
    }
}
