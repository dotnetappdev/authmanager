namespace AuthManager.Core.Models;

/// <summary>A named collection of roles assigned as a unit to users.</summary>
public sealed class GroupDto
{
    public string   Id          { get; set; } = "";
    public string   Name        { get; set; } = "";
    public string?  Description { get; set; }
    public int      MemberCount { get; set; }
    public List<string> Roles   { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CreateGroupDto
{
    public string   Name        { get; set; } = "";
    public string?  Description { get; set; }
    public List<string> Roles   { get; set; } = [];
}

public sealed class UpdateGroupDto
{
    public string   Name        { get; set; } = "";
    public string?  Description { get; set; }
    public List<string> Roles   { get; set; } = [];
}
