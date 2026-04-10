namespace AuthManager.Core.Models;

/// <summary>
/// Data transfer object for user information.
/// </summary>
public sealed class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool LockoutEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool IsLockedOut => LockoutEnd.HasValue && LockoutEnd.Value > DateTimeOffset.UtcNow;
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public List<string> Roles { get; set; } = [];
    public List<ClaimDto> Claims { get; set; } = [];

    /// <summary>
    /// Custom user attributes stored as claims with a "custom:" prefix.
    /// Surfaced here for easy access.
    /// The key is the attribute name (without prefix), the value is its current value.
    /// </summary>
    public Dictionary<string, string> CustomAttributes { get; set; } = [];

    /// <summary>
    /// Required actions the user must complete on their next sign-in.
    /// E.g. VerifyEmail, UpdatePassword, ConfigureTOTP.
    /// Stored as a "required_action" claim per entry.
    /// </summary>
    public List<string> RequiredActions { get; set; } = [];

    /// <summary>Number of currently tracked active sessions for this user.</summary>
    public int ActiveSessionCount { get; set; }

    public Dictionary<string, string?> AdditionalProperties { get; set; } = [];
}

/// <summary>
/// DTO for creating a new user.
/// </summary>
public sealed class CreateUserDto
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool SendConfirmationEmail { get; set; }
    public List<string> Roles { get; set; } = [];
    public List<ClaimDto> Claims { get; set; } = [];
}

/// <summary>
/// DTO for updating an existing user.
/// </summary>
public sealed class UpdateUserDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool LockoutEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
}

/// <summary>
/// DTO for resetting a user's password.
/// </summary>
public sealed class ResetPasswordDto
{
    public string UserId { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public bool RequirePasswordChange { get; set; }
}

/// <summary>
/// Filter parameters for user queries.
/// </summary>
public sealed class UserFilter
{
    public string? SearchTerm { get; set; }
    public string? Role { get; set; }
    public bool? IsLockedOut { get; set; }
    public bool? EmailConfirmed { get; set; }
    public bool? TwoFactorEnabled { get; set; }
    public bool? HasRequiredActions { get; set; }
    /// <summary>Filter by a specific custom attribute value. Key = attribute key, Value = value to match.</summary>
    public Dictionary<string, string>? AttributeFilter { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public string SortBy { get; set; } = "UserName";
    public bool SortDescending { get; set; }
}
