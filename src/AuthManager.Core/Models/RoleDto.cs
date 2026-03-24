namespace AuthManager.Core.Models;

/// <summary>
/// Data transfer object for roles.
/// </summary>
public sealed class RoleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<ClaimDto> Claims { get; set; } = [];
    public int UserCount { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
}

/// <summary>
/// DTO for creating a role.
/// </summary>
public sealed class CreateRoleDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<ClaimDto> Claims { get; set; } = [];
}

/// <summary>
/// DTO for updating a role.
/// </summary>
public sealed class UpdateRoleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
