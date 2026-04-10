namespace AuthManager.Core.Options;

/// <summary>
/// Password complexity and rotation policy — equivalent to the Password Policy settings UI.
///
/// These settings are applied to the ASP.NET Identity <c>PasswordOptions</c> automatically
/// when <c>AddAuthManager()</c> is called. You do NOT need to repeat them in
/// <c>AddIdentity()</c>.
/// </summary>
public sealed class PasswordPolicyOptions
{
    /// <summary>Minimum password length. Default: 8.</summary>
    public int MinimumLength { get; set; } = 8;

    /// <summary>Maximum password length. Default: 128. Prevents DoS via bcrypt.</summary>
    public int MaximumLength { get; set; } = 128;

    /// <summary>Require at least one uppercase letter (A–Z). Default: true.</summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>Require at least one lowercase letter (a–z). Default: true.</summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>Require at least one digit (0–9). Default: true.</summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>Require at least one non-alphanumeric character (!, @, #…). Default: true.</summary>
    public bool RequireNonAlphanumeric { get; set; } = true;

    /// <summary>
    /// Prevent users from reusing their last N passwords.
    /// Requires the consuming app to store password hashes — AuthManager records hashes
    /// as a user claim (<c>password_history</c>) automatically when this is &gt; 0.
    /// Default: 0 (disabled).
    /// </summary>
    public int PasswordHistoryCount { get; set; } = 0;

    /// <summary>
    /// Force password expiry after N days. 0 = never expires.
    /// When expired the user is flagged with <see cref="RequiredUserAction.UpdatePassword"/>.
    /// Default: 0.
    /// </summary>
    public int PasswordExpiryDays { get; set; } = 0;

    /// <summary>
    /// Prevent the password containing the user's username. Default: true.
    /// </summary>
    public bool DenyUsernameInPassword { get; set; } = true;
}
