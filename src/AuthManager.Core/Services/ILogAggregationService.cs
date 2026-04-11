using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Service for aggregating and querying log entries from Serilog sinks.
/// </summary>
public interface ILogAggregationService
{
    Task<PagedResult<LogEntry>> GetLogsAsync(LogFilter filter, CancellationToken ct = default);
    Task<List<string>> GetSourceContextsAsync(CancellationToken ct = default);
    Task ClearLogsAsync(CancellationToken ct = default);
    IAsyncEnumerable<LogEntry> StreamLogsAsync(CancellationToken ct = default);
}

/// <summary>
/// Service for recording and querying audit log entries.
/// </summary>
public interface IAuditService
{
    Task RecordAsync(AuditEntry entry, CancellationToken ct = default);
    Task<PagedResult<AuditEntry>> GetAuditLogAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);
}

/// <summary>
/// Service for managing OAuth providers.
/// </summary>
public interface IOAuthProviderService
{
    Task<List<OAuthProviderInfo>> GetProvidersAsync(CancellationToken ct = default);
    Task<OAuthProviderInfo?> GetProviderAsync(string providerName, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> UpdateProviderAsync(UpdateOAuthProviderDto dto, CancellationToken ct = default);
}

/// <summary>
/// Service for managing JWT configuration.
/// </summary>
public interface IJwtConfigService
{
    Task<JwtConfigInfo> GetConfigAsync(CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> UpdateConfigAsync(JwtConfigInfo config, CancellationToken ct = default);
    Task<string> GenerateTestTokenAsync(string userId, CancellationToken ct = default);
}

/// <summary>
/// JWT configuration information shown in the UI.
/// </summary>
public sealed class JwtConfigInfo
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKeyPreview { get; set; } = string.Empty;
    public int AccessTokenExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 7;
    public bool EnableRefreshTokens { get; set; } = true;
    public string Algorithm { get; set; } = "HS256";
    public bool RequireHttpsMetadata { get; set; } = true;
    public bool ValidateAudience { get; set; } = true;
    public bool ValidateIssuer { get; set; } = true;
    public bool ValidateLifetime { get; set; } = true;
}

/// <summary>
/// Service for managing SSO (Single Sign-On) providers —
/// Entra ID (Azure AD), generic OIDC, and SAML 2.0.
/// </summary>
public interface ISsoService
{
    /// <summary>Returns the list of all configured SSO providers and their status.</summary>
    Task<List<SsoProviderInfo>> GetProvidersAsync(CancellationToken ct = default);

    /// <summary>Returns a single provider by key.</summary>
    Task<SsoProviderInfo?> GetProviderAsync(string key, CancellationToken ct = default);

    /// <summary>Persists updated settings for a provider.</summary>
    Task<(bool Success, string[] Errors)> UpdateProviderAsync(UpdateSsoProviderDto dto, CancellationToken ct = default);
}

/// <summary>
/// Service for generating and verifying one-time passwords (OTP) used in
/// passwordless login and step-up authentication flows.
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generate a new OTP code for the given user and purpose.
    /// If a non-expired code for the same user/purpose already exists and the
    /// resend cooldown has not elapsed, returns an error.
    /// </summary>
    /// <param name="userId">Identity user ID the code belongs to.</param>
    /// <param name="purpose">
    /// Logical purpose: "login", "email-verify", "step-up", etc.
    /// Used to namespace codes so a login code cannot satisfy an email-verify check.
    /// </param>
    Task<OtpGenerateResult> GenerateAsync(string userId, string purpose, CancellationToken ct = default);

    /// <summary>
    /// Verify a code supplied by the user.
    /// Consumes the code on success. Increments the attempt counter on failure.
    /// </summary>
    Task<OtpVerifyResult> VerifyAsync(string userId, string purpose, string code, CancellationToken ct = default);

    /// <summary>
    /// Returns the current OTP settings and today's usage statistics.
    /// </summary>
    Task<OtpSettingsInfo> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>Persists updated OTP settings.</summary>
    Task<(bool Success, string[] Errors)> UpdateSettingsAsync(OtpSettingsInfo settings, CancellationToken ct = default);

    /// <summary>Invalidate (expire) all active OTP codes for the specified user.</summary>
    Task RevokeAllAsync(string userId, CancellationToken ct = default);
}
