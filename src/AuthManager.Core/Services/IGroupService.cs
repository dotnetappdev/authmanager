using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Manages groups — named collections of roles that can be assigned to users in bulk.
/// </summary>
public interface IGroupService
{
    Task<List<GroupDto>>  GetGroupsAsync(CancellationToken ct = default);
    Task<GroupDto?>       GetGroupAsync(string id, CancellationToken ct = default);

    Task<(bool Success, string[] Errors)> CreateGroupAsync(CreateGroupDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> UpdateGroupAsync(string id, UpdateGroupDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> DeleteGroupAsync(string id, CancellationToken ct = default);

    /// <summary>Adds a user to the group and grants them all of the group's roles.</summary>
    Task<(bool Success, string[] Errors)> AddUserToGroupAsync(string groupId, string userId, CancellationToken ct = default);
    /// <summary>Removes a user from the group and revokes the group's roles (unless held via another group).</summary>
    Task<(bool Success, string[] Errors)> RemoveUserFromGroupAsync(string groupId, string userId, CancellationToken ct = default);

    Task<List<UserDto>>   GetGroupMembersAsync(string groupId, CancellationToken ct = default);
    Task<List<GroupDto>>  GetUserGroupsAsync(string userId, CancellationToken ct = default);
}
