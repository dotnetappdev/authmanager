namespace AuthManager.Core.Models;

/// <summary>
/// OTP administration overview shown on the OTP Settings admin page.
/// </summary>
public sealed class OtpSettingsInfo
{
    /// <summary>Whether email OTP is globally enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Numeric code length.</summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>Code validity window in minutes.</summary>
    public int CodeExpiryMinutes { get; set; } = 10;

    /// <summary>Max failed attempts before code is invalidated.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Minimum seconds between resend requests.</summary>
    public int ResendCooldownSeconds { get; set; } = 60;

    /// <summary>Whether codes are alphanumeric (rather than purely numeric).</summary>
    public bool UseAlphanumericCodes { get; set; }

    /// <summary>Email subject template.</summary>
    public string EmailSubject { get; set; } = string.Empty;

    /// <summary>Email body template.</summary>
    public string EmailBodyTemplate { get; set; } = string.Empty;

    // ── Statistics (read-only) ──────────────────────────────────────────
    public int TotalIssuedToday { get; set; }
    public int TotalVerifiedToday { get; set; }
    public int TotalExpiredToday { get; set; }
    public int TotalPending { get; set; }
}

/// <summary>
/// Result returned after calling <see cref="IOtpService.GenerateAsync"/>.
/// </summary>
public sealed class OtpGenerateResult
{
    public bool Success { get; set; }
    public string? PlainCode { get; set; }   // Only set on success; caller sends it to the user
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Result returned after calling <see cref="IOtpService.VerifyAsync"/>.
/// </summary>
public sealed class OtpVerifyResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    /// <summary>How many attempts remain before the code is invalidated.</summary>
    public int AttemptsRemaining { get; set; }
}
