using AuthManager.Core.Options;

namespace AuthManager.Core.Services;

/// <summary>
/// Reads and updates password complexity, rotation, lockout, and brute-force detection
/// settings at runtime — equivalent to Keycloak's "Password Policy" and
/// "Brute Force Detection" realm configuration tabs.
///
/// The default in-memory implementation stores overrides for the current process
/// lifetime. Replace with a database-backed implementation by registering
/// <c>ISecurityPolicyService</c> after <c>AddAuthManager()</c>.
/// </summary>
public interface ISecurityPolicyService
{
    /// <summary>Get the currently effective password policy.</summary>
    PasswordPolicyOptions GetPasswordPolicy();

    /// <summary>
    /// Persist an updated password policy.
    /// The new values are used by the AuthManager UI immediately.
    /// Note: ASP.NET Identity's built-in validation uses the options configured at startup;
    /// call <c>AddAuthManager()</c> with the desired values to apply them at the
    /// Identity layer from the start.
    /// </summary>
    Task UpdatePasswordPolicyAsync(PasswordPolicyOptions policy, CancellationToken ct = default);

    /// <summary>Get the currently effective security / lockout policy.</summary>
    SecurityPolicyOptions GetSecurityPolicy();

    /// <summary>Persist an updated security / lockout policy.</summary>
    Task UpdateSecurityPolicyAsync(SecurityPolicyOptions policy, CancellationToken ct = default);
}
