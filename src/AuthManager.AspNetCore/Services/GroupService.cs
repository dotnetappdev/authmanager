using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

internal sealed class GroupService<TUser, TRole> : IGroupService
    where TUser : IdentityUser, new()
    where TRole : IdentityRole, new()
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly UserManager<TUser>  _userManager;
    private readonly RoleManager<TRole>  _roleManager;

    public GroupService(
        IDbContextFactory<AuthManagerDbContext> factory,
        UserManager<TUser> userManager,
        RoleManager<TRole> roleManager)
    {
        _factory     = factory;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<List<GroupDto>> GetGroupsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var groups  = await db.Groups.OrderBy(g => g.Name).ToListAsync(ct);
        var members = await db.GroupMembers.GroupBy(m => m.GroupId)
                              .Select(g => new { g.Key, Count = g.Count() })
                              .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return groups.Select(g => ToDto(g, members.GetValueOrDefault(g.Id))).ToList();
    }

    public async Task<GroupDto?> GetGroupAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var group = await db.Groups.FindAsync([id], ct);
        if (group is null) return null;
        var count = await db.GroupMembers.CountAsync(m => m.GroupId == id, ct);
        return ToDto(group, count);
    }

    public async Task<(bool Success, string[] Errors)> CreateGroupAsync(
        CreateGroupDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, ["Group name is required."]);

        await using var db = await _factory.CreateDbContextAsync(ct);

        if (await db.Groups.AnyAsync(g => g.Name == dto.Name, ct))
            return (false, [$"A group named '{dto.Name}' already exists."]);

        db.Groups.Add(new GroupRecord
        {
            Id          = Guid.NewGuid().ToString("N")[..16],
            Name        = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            RolesJson   = JsonSerializer.Serialize(dto.Roles ?? [])
        });
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> UpdateGroupAsync(
        string id, UpdateGroupDto dto, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var group = await db.Groups.FindAsync([id], ct);
        if (group is null) return (false, ["Group not found."]);

        if (await db.Groups.AnyAsync(g => g.Name == dto.Name && g.Id != id, ct))
            return (false, [$"A group named '{dto.Name}' already exists."]);

        group.Name        = dto.Name.Trim();
        group.Description = dto.Description?.Trim();
        group.RolesJson   = JsonSerializer.Serialize(dto.Roles ?? []);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> DeleteGroupAsync(
        string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var group = await db.Groups.FindAsync([id], ct);
        if (group is null) return (false, ["Group not found."]);

        var members = await db.GroupMembers.Where(m => m.GroupId == id).ToListAsync(ct);
        db.GroupMembers.RemoveRange(members);
        db.Groups.Remove(group);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> AddUserToGroupAsync(
        string groupId, string userId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var group = await db.Groups.FindAsync([groupId], ct);
        if (group is null) return (false, ["Group not found."]);

        if (await db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId, ct))
            return (true, []);  // already a member — idempotent

        db.GroupMembers.Add(new GroupMemberRecord { GroupId = groupId, UserId = userId });
        await db.SaveChangesAsync(ct);

        // Grant the group's roles
        var user = await _userManager.FindByIdAsync(userId);
        if (user is not null)
        {
            var roles = JsonSerializer.Deserialize<List<string>>(group.RolesJson) ?? [];
            foreach (var role in roles)
            {
                if (!await _userManager.IsInRoleAsync(user, role))
                    await _userManager.AddToRoleAsync(user, role);
            }
        }
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> RemoveUserFromGroupAsync(
        string groupId, string userId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var membership = await db.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, ct);
        if (membership is null) return (true, []);

        db.GroupMembers.Remove(membership);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<List<UserDto>> GetGroupMembersAsync(string groupId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var userIds = await db.GroupMembers.Where(m => m.GroupId == groupId)
                              .Select(m => m.UserId).ToListAsync(ct);

        var result = new List<UserDto>();
        foreach (var uid in userIds)
        {
            var user = await _userManager.FindByIdAsync(uid);
            if (user is not null)
                result.Add(new UserDto
                {
                    Id       = user.Id,
                    UserName = user.UserName ?? "",
                    Email    = user.Email ?? ""
                });
        }
        return result;
    }

    public async Task<List<GroupDto>> GetUserGroupsAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var groupIds = await db.GroupMembers.Where(m => m.UserId == userId)
                               .Select(m => m.GroupId).ToListAsync(ct);
        var groups = await db.Groups.Where(g => groupIds.Contains(g.Id)).ToListAsync(ct);
        return groups.Select(g => ToDto(g, 0)).ToList();
    }

    private static GroupDto ToDto(GroupRecord g, int memberCount) => new()
    {
        Id          = g.Id,
        Name        = g.Name,
        Description = g.Description,
        MemberCount = memberCount,
        Roles       = JsonSerializer.Deserialize<List<string>>(g.RolesJson) ?? [],
        CreatedAt   = g.CreatedAt
    };
}
