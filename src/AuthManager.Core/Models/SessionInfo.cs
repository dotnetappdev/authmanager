namespace AuthManager.Core.Models;

/// <summary>
/// Represents a tracked user session (browser/device sign-in).
/// Displayed in the user detail page "Active Sessions" panel,
/// displayed in the Active Sessions panel.
/// </summary>
public sealed class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActiveAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Remote IP address of the client.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Raw User-Agent string from the browser/app.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Friendly device/browser description parsed from the User-Agent.</summary>
    public string? DeviceDescription { get; set; }

    /// <summary>ISO 3166-1 country code inferred from IP geo-lookup (optional).</summary>
    public string? CountryCode { get; set; }

    public bool IsCurrent { get; set; }
}

/// <summary>
/// Required actions that will be enforced on the user's next sign-in.
/// Equivalent to the Required Actions panel.
/// </summary>
public enum RequiredUserAction
{
    /// <summary>User must verify their email address.</summary>
    VerifyEmail,

    /// <summary>User must change their password.</summary>
    UpdatePassword,

    /// <summary>User must configure TOTP / authenticator app.</summary>
    ConfigureTOTP,

    /// <summary>User must complete their profile (display name, phone, etc.).</summary>
    UpdateProfile,

    /// <summary>User must accept updated terms of service.</summary>
    AcceptTerms,
}

/// <summary>
/// Summary result from a user import operation.
/// </summary>
public sealed class ImportResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = [];
    public bool Success => Failed == 0;
}

/// <summary>
/// Options that control behaviour during a bulk user import.
/// </summary>
public sealed class ImportOptions
{
    /// <summary>
    /// Update existing users found by email instead of skipping them.
    /// Default: false (skip duplicates).
    /// </summary>
    public bool UpdateExisting { get; set; } = false;

    /// <summary>Send a welcome/verification email to each imported user. Default: false.</summary>
    public bool SendWelcomeEmail { get; set; } = false;

    /// <summary>Assign these roles to every imported user (in addition to any in the file).</summary>
    public List<string> DefaultRoles { get; set; } = [];
}
