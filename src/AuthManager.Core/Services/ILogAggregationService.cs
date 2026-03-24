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
