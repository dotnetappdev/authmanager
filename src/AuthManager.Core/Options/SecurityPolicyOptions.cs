namespace AuthManager.Core.Options;

/// <summary>
/// Security hardening settings — maps to account lockout and
/// "Sessions" configuration panels.
/// </summary>
public sealed class SecurityPolicyOptions
{
    // ── Brute-force protection ─────────────────────────────────────────────────

    /// <summary>
    /// Enable brute-force / credential-stuffing detection.
    /// When enabled, accounts are locked after <see cref="MaxFailedLoginAttempts"/> failures.
    /// Default: true.
    /// </summary>
    public bool EnableBruteForceDetection { get; set; } = true;

    /// <summary>
    /// Number of consecutive failed login attempts before an account is locked.
    /// Maps to <c>IdentityOptions.Lockout.MaxFailedAccessAttempts</c>.
    /// Default: 5.
    /// </summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    /// <summary>
    /// How long an account stays locked after hitting <see cref="MaxFailedLoginAttempts"/>.
    /// Default: 15 minutes.
    /// </summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Reset the failed-attempt counter after this window of no attempts.
    /// Default: 12 hours.
    /// </summary>
    public TimeSpan FailedAttemptWindow { get; set; } = TimeSpan.FromHours(12);

    // ── Session policy ────────────────────────────────────────────────────────

    /// <summary>
    /// Invalidate all existing sessions when a user changes their password.
    /// Achieved by updating the ASP.NET Identity security stamp.
    /// Default: true.
    /// </summary>
    public bool InvalidateSessionsOnPasswordChange { get; set; } = true;

    /// <summary>
    /// How often the security stamp is validated against the server (sliding).
    /// Shorter = more responsive revocation; longer = better performance.
    /// Default: 30 minutes.
    /// </summary>
    public TimeSpan SecurityStampValidationInterval { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Maximum number of concurrent active sessions per user.
    /// 0 = unlimited. When the limit is exceeded the oldest session is terminated.
    /// Default: 0.
    /// </summary>
    public int MaxConcurrentSessions { get; set; } = 0;

    // ── Registration ─────────────────────────────────────────────────────────

    /// <summary>
    /// Allow new user self-registration through the standard Identity flows.
    /// Default: true.
    /// </summary>
    public bool AllowSelfRegistration { get; set; } = true;

    /// <summary>
    /// Require email verification before a newly registered account can sign in.
    /// Default: false.
    /// </summary>
    public bool RequireEmailVerificationOnRegistration { get; set; } = false;

    // ── Token security ────────────────────────────────────────────────────────

    /// <summary>
    /// Block re-use of a refresh token after it has been used once (rotation).
    /// Default: true.
    /// </summary>
    public bool EnableRefreshTokenRotation { get; set; } = true;

    /// <summary>
    /// Reject tokens whose IP address does not match the issuing IP.
    /// Use cautiously — may break mobile clients behind NAT.
    /// Default: false.
    /// </summary>
    public bool BindTokenToIpAddress { get; set; } = false;
}
