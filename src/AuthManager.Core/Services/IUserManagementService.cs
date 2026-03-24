using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Service for managing users via ASP.NET Identity.
/// </summary>
public interface IUserManagementService
{
    Task<PagedResult<UserDto>> GetUsersAsync(UserFilter filter, CancellationToken ct = default);
    Task<UserDto?> GetUserByIdAsync(string userId, CancellationToken ct = default);
    Task<UserDto?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    Task<UserDto?> GetUserByUserNameAsync(string userName, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> CreateUserAsync(CreateUserDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> UpdateUserAsync(UpdateUserDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> DeleteUserAsync(string userId, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> LockUserAsync(string userId, DateTimeOffset? until = null, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> UnlockUserAsync(string userId, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> AssignRoleAsync(string userId, string roleName, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> RemoveRoleAsync(string userId, string roleName, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> AddClaimAsync(string userId, ClaimDto claim, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> RemoveClaimAsync(string userId, ClaimDto claim, CancellationToken ct = default);
    Task<bool> SendConfirmationEmailAsync(string userId, CancellationToken ct = default);
    Task<DashboardStats> GetDashboardStatsAsync(CancellationToken ct = default);

    // ── Required Actions (Keycloak-style) ────────────────────────────────────

    /// <summary>
    /// Add a required action that the user must complete on their next sign-in
    /// (e.g. "UpdatePassword", "ConfigureTOTP", "VerifyEmail").
    /// Stored as an ASP.NET Identity claim with type "required_action".
    /// </summary>
    Task<(bool Success, string[] Errors)> AddRequiredActionAsync(string userId, string action, CancellationToken ct = default);

    /// <summary>Remove a previously assigned required action.</summary>
    Task<(bool Success, string[] Errors)> RemoveRequiredActionAsync(string userId, string action, CancellationToken ct = default);

    /// <summary>
    /// Return the list of required action strings currently assigned to a user.
    /// Returns an empty list if the user has none.
    /// </summary>
    Task<List<string>> GetRequiredActionsAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// High-level statistics shown on the dashboard.
/// </summary>
public sealed class DashboardStats
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int LockedOutUsers { get; set; }
    public int UnconfirmedEmailUsers { get; set; }
    public int TotalRoles { get; set; }
    public int TotalClaims { get; set; }
    public int RecentLogins { get; set; }
    public List<UserActivityEntry> RecentActivity { get; set; } = [];
}

public sealed class UserActivityEntry
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}
