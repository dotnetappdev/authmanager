using System.Security.Claims;
using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

internal sealed class TenantService<TUser> : ITenantService
    where TUser : IdentityUser, new()
{
    private const string RootTenantId = "";

    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly UserManager<TUser> _userManager;
    private readonly MultiTenancyOptions _options;

    public TenantService(
        IDbContextFactory<AuthManagerDbContext> factory,
        UserManager<TUser> userManager,
        IOptions<AuthManagerOptions> options)
    {
        _factory      = factory;
        _userManager  = userManager;
        _options      = options.Value.MultiTenancy;
    }

    private string ClaimType => string.IsNullOrWhiteSpace(_options.TenantClaimType) ? "tenant_id" : _options.TenantClaimType;

    public async Task<List<TenantDto>> GetTenantsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await EnsureSeededAsync(db, ct);

        var tenants = await db.Tenants.OrderBy(t => t.DisplayName).ToListAsync(ct);
        var result = new List<TenantDto>();
        var assignedCount = 0;

        foreach (var tenant in tenants)
        {
            var members = await _userManager.GetUsersForClaimAsync(new Claim(ClaimType, tenant.Id));
            assignedCount += members.Count;
            result.Add(ToDto(tenant, members.Count));
        }

        if (_options.AllowRootTenant)
        {
            var totalUsers = await _userManager.Users.CountAsync(ct);
            result.Insert(0, new TenantDto
            {
                Id = RootTenantId,
                DisplayName = "Root (unassigned)",
                MemberCount = Math.Max(0, totalUsers - assignedCount),
                IsRootTenant = true
            });
        }

        return result;
    }

    public async Task<TenantDto?> GetTenantAsync(string id, CancellationToken ct = default)
    {
        if (id == RootTenantId && _options.AllowRootTenant)
            return (await GetTenantsAsync(ct)).FirstOrDefault(t => t.IsRootTenant);

        await using var db = await _factory.CreateDbContextAsync(ct);
        var tenant = await db.Tenants.FindAsync([id], ct);
        if (tenant is null) return null;

        var members = await _userManager.GetUsersForClaimAsync(new Claim(ClaimType, tenant.Id));
        return ToDto(tenant, members.Count);
    }

    public async Task<(bool Success, string[] Errors)> CreateTenantAsync(CreateTenantDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
            return (false, ["Tenant ID is required."]);
        if (string.IsNullOrWhiteSpace(dto.DisplayName))
            return (false, ["Display name is required."]);

        var id = dto.Id.Trim();
        await using var db = await _factory.CreateDbContextAsync(ct);

        if (await db.Tenants.AnyAsync(t => t.Id == id, ct))
            return (false, [$"A tenant with ID '{id}' already exists."]);

        db.Tenants.Add(new TenantRecord
        {
            Id           = id,
            DisplayName  = dto.DisplayName.Trim(),
            MetadataJson = JsonSerializer.Serialize(dto.Metadata ?? [])
        });
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> UpdateTenantAsync(string id, UpdateTenantDto dto, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var tenant = await db.Tenants.FindAsync([id], ct);
        if (tenant is null) return (false, ["Tenant not found."]);

        if (string.IsNullOrWhiteSpace(dto.DisplayName))
            return (false, ["Display name is required."]);

        tenant.DisplayName  = dto.DisplayName.Trim();
        tenant.MetadataJson = JsonSerializer.Serialize(dto.Metadata ?? []);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> DeleteTenantAsync(string id, CancellationToken ct = default)
    {
        if (id == RootTenantId)
            return (false, ["The root tenant cannot be deleted."]);

        await using var db = await _factory.CreateDbContextAsync(ct);
        var tenant = await db.Tenants.FindAsync([id], ct);
        if (tenant is null) return (false, ["Tenant not found."]);

        // Members are not deleted — they simply fall back to the root tenant.
        var members = await _userManager.GetUsersForClaimAsync(new Claim(ClaimType, id));
        foreach (var member in members)
            await _userManager.RemoveClaimAsync(member, new Claim(ClaimType, id));

        db.Tenants.Remove(tenant);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<List<UserDto>> GetTenantMembersAsync(string tenantId, CancellationToken ct = default)
    {
        if (tenantId == RootTenantId && _options.AllowRootTenant)
        {
            var allUsers = await _userManager.Users.ToListAsync(ct);
            var result = new List<UserDto>();
            foreach (var user in allUsers)
            {
                var claims = await _userManager.GetClaimsAsync(user);
                if (!claims.Any(c => c.Type == ClaimType))
                    result.Add(ToUserDto(user));
            }
            return result;
        }

        var members = await _userManager.GetUsersForClaimAsync(new Claim(ClaimType, tenantId));
        return members.Select(ToUserDto).ToList();
    }

    public async Task<string?> GetUserTenantIdAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return null;

        var claims = await _userManager.GetClaimsAsync(user);
        return claims.FirstOrDefault(c => c.Type == ClaimType)?.Value;
    }

    public async Task<(bool Success, string[] Errors)> AssignUserToTenantAsync(string userId, string tenantId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return (false, ["User not found."]);

        await using var db = await _factory.CreateDbContextAsync(ct);
        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId, ct))
            return (false, ["Tenant not found."]);

        var existing = await _userManager.GetClaimsAsync(user);
        foreach (var claim in existing.Where(c => c.Type == ClaimType))
            await _userManager.RemoveClaimAsync(user, claim);

        var result = await _userManager.AddClaimAsync(user, new Claim(ClaimType, tenantId));
        return result.Succeeded
            ? (true, [])
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<(bool Success, string[] Errors)> RemoveUserFromTenantAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return (false, ["User not found."]);

        var existing = await _userManager.GetClaimsAsync(user);
        foreach (var claim in existing.Where(c => c.Type == ClaimType))
            await _userManager.RemoveClaimAsync(user, claim);

        return (true, []);
    }

    /// <summary>Inserts tenants pre-defined in <c>MultiTenancyOptions.Tenants</c> if the store is empty.</summary>
    private async Task EnsureSeededAsync(AuthManagerDbContext db, CancellationToken ct)
    {
        if (_options.Tenants.Count == 0) return;
        if (await db.Tenants.AnyAsync(ct)) return;

        foreach (var predefined in _options.Tenants)
        {
            if (string.IsNullOrWhiteSpace(predefined.Id)) continue;

            db.Tenants.Add(new TenantRecord
            {
                Id           = predefined.Id.Trim(),
                DisplayName  = string.IsNullOrWhiteSpace(predefined.DisplayName) ? predefined.Id : predefined.DisplayName.Trim(),
                MetadataJson = JsonSerializer.Serialize(predefined.Metadata ?? [])
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static TenantDto ToDto(TenantRecord t, int memberCount) => new()
    {
        Id          = t.Id,
        DisplayName = t.DisplayName,
        MemberCount = memberCount,
        Metadata    = JsonSerializer.Deserialize<Dictionary<string, string>>(t.MetadataJson) ?? [],
        CreatedAt   = t.CreatedAt
    };

    private static UserDto ToUserDto(TUser user) => new()
    {
        Id       = user.Id,
        UserName = user.UserName ?? "",
        Email    = user.Email ?? ""
    };
}
