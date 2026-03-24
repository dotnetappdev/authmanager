namespace AuthManager.Core.Models;

/// <summary>
/// An audit log entry recording changes made via the AuthManager UI.
/// </summary>
public sealed class AuditEntry
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? PerformedByUserId { get; set; }
    public string? PerformedByUserName { get; set; }
    public string? IpAddress { get; set; }
    public Dictionary<string, object?> OldValues { get; set; } = [];
    public Dictionary<string, object?> NewValues { get; set; } = [];
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public static class AuditActions
{
    public const string UserCreated = "UserCreated";
    public const string UserUpdated = "UserUpdated";
    public const string UserDeleted = "UserDeleted";
    public const string UserLockedOut = "UserLockedOut";
    public const string UserUnlocked = "UserUnlocked";
    public const string PasswordReset = "PasswordReset";
    public const string RoleCreated = "RoleCreated";
    public const string RoleUpdated = "RoleUpdated";
    public const string RoleDeleted = "RoleDeleted";
    public const string RoleAssigned = "RoleAssigned";
    public const string RoleRemoved = "RoleRemoved";
    public const string ClaimAdded = "ClaimAdded";
    public const string ClaimRemoved = "ClaimRemoved";
    public const string OAuthConfigUpdated = "OAuthConfigUpdated";
    public const string JwtConfigUpdated = "JwtConfigUpdated";
}
