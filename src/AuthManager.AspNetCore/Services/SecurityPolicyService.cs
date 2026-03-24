using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// In-memory security policy service.
///
/// On first access the values are read from <see cref="AuthManagerOptions"/> (which
/// in turn is populated from your <c>appsettings.json</c> / <c>AddAuthManager()</c>
/// configuration). Runtime updates are stored in memory for the lifetime of the
/// process.
///
/// For persistence across restarts, replace this with a database-backed
/// implementation by registering <c>ISecurityPolicyService</c> after
/// <c>AddAuthManager()</c>.
/// </summary>
public sealed class SecurityPolicyService : ISecurityPolicyService
{
    private readonly IOptionsMonitor<AuthManagerOptions> _monitor;

    // Mutable runtime copies — null means "use the configured default"
    private PasswordPolicyOptions? _passwordPolicy;
    private SecurityPolicyOptions? _securityPolicy;

    public SecurityPolicyService(IOptionsMonitor<AuthManagerOptions> monitor)
    {
        _monitor = monitor;
    }

    public PasswordPolicyOptions GetPasswordPolicy()
        => _passwordPolicy ?? Clone(_monitor.CurrentValue.PasswordPolicy);

    public Task UpdatePasswordPolicyAsync(PasswordPolicyOptions policy, CancellationToken ct = default)
    {
        _passwordPolicy = policy;
        return Task.CompletedTask;
    }

    public SecurityPolicyOptions GetSecurityPolicy()
        => _securityPolicy ?? Clone(_monitor.CurrentValue.SecurityPolicy);

    public Task UpdateSecurityPolicyAsync(SecurityPolicyOptions policy, CancellationToken ct = default)
    {
        _securityPolicy = policy;
        return Task.CompletedTask;
    }

    // Shallow-clone so callers can mutate the returned object safely
    private static PasswordPolicyOptions Clone(PasswordPolicyOptions src) => new()
    {
        MinimumLength          = src.MinimumLength,
        MaximumLength          = src.MaximumLength,
        RequireUppercase       = src.RequireUppercase,
        RequireLowercase       = src.RequireLowercase,
        RequireDigit           = src.RequireDigit,
        RequireNonAlphanumeric = src.RequireNonAlphanumeric,
        PasswordHistoryCount   = src.PasswordHistoryCount,
        PasswordExpiryDays     = src.PasswordExpiryDays,
        DenyUsernameInPassword = src.DenyUsernameInPassword,
    };

    private static SecurityPolicyOptions Clone(SecurityPolicyOptions src) => new()
    {
        EnableBruteForceDetection          = src.EnableBruteForceDetection,
        MaxFailedLoginAttempts             = src.MaxFailedLoginAttempts,
        LockoutDuration                    = src.LockoutDuration,
        FailedAttemptWindow                = src.FailedAttemptWindow,
        InvalidateSessionsOnPasswordChange = src.InvalidateSessionsOnPasswordChange,
        SecurityStampValidationInterval    = src.SecurityStampValidationInterval,
        MaxConcurrentSessions              = src.MaxConcurrentSessions,
        AllowSelfRegistration              = src.AllowSelfRegistration,
        RequireEmailVerificationOnRegistration = src.RequireEmailVerificationOnRegistration,
        EnableRefreshTokenRotation         = src.EnableRefreshTokenRotation,
        BindTokenToIpAddress               = src.BindTokenToIpAddress,
    };
}
