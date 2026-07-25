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

    // ── Required Actions ────────────────────────────────────

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

    // ── Two-Factor Authentication ─────────────────────────────

    /// <summary>Disable 2FA for the given user. Their authenticator key is NOT cleared.</summary>
    Task<(bool Success, string[] Errors)> DisableTwoFactorAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Reset the TOTP authenticator key. The user must re-enroll on their next sign-in.
    /// Also sets the "ConfigureTOTP" required action.
    /// </summary>
    Task<(bool Success, string[] Errors)> ResetAuthenticatorAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Force-require 2FA setup for the given user.
    /// Adds the "ConfigureTOTP" required action without disabling any existing 2FA.
    /// </summary>
    Task<(bool Success, string[] Errors)> Force2FaEnrollmentAsync(string userId, CancellationToken ct = default);

    /// <summary>Return users grouped by their 2FA status for the admin overview.</summary>
    Task<TwoFactorStats> GetTwoFactorStatsAsync(CancellationToken ct = default);

    /// <summary>
    /// Generates a fresh set of 2FA recovery (backup) codes for the user, invalidating any
    /// previous set. Requires two-factor authentication to already be enabled.
    /// The returned codes are shown once — they are stored hashed and cannot be retrieved again.
    /// </summary>
    Task<(bool Success, string[] Errors, string[]? Codes)> GenerateRecoveryCodesAsync(string userId, int count = 10, CancellationToken ct = default);

    /// <summary>Number of unused recovery codes remaining for the user.</summary>
    Task<int> GetRecoveryCodesRemainingAsync(string userId, CancellationToken ct = default);

    // ── Temporary (expiring) role assignments ──────────────────

    /// <summary>
    /// Grants a role that is automatically revoked at <paramref name="expiresAt"/>.
    /// A background sweep removes the role once it expires — see
    /// <c>SecurityPolicyOptions.RoleExpiryCheckInterval</c>. If the user already holds the
    /// role permanently, this has no effect on the permanent grant.
    /// </summary>
    Task<(bool Success, string[] Errors)> AssignTemporaryRoleAsync(string userId, string roleName, DateTimeOffset expiresAt, CancellationToken ct = default);

    /// <summary>Removes the expiry from a role grant, making it permanent. The role itself is not removed.</summary>
    Task<(bool Success, string[] Errors)> MakeRoleAssignmentPermanentAsync(string userId, string roleName, CancellationToken ct = default);

    /// <summary>Returns the role names currently scheduled to expire for a user, keyed by role name.</summary>
    Task<Dictionary<string, DateTimeOffset>> GetRoleExpiriesAsync(string userId, CancellationToken ct = default);
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

public sealed class TwoFactorStats
{
    public int TotalUsers   { get; set; }
    public int Enabled2FA   { get; set; }
    public int Disabled2FA  { get; set; }
    public int PendingEnroll { get; set; }  // users with ConfigureTOTP required action
}

public sealed class UserActivityEntry
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
}
