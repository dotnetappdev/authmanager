using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// SQLite-backed security policy service.
/// On first access reads from the database; falls back to <see cref="AuthManagerOptions"/>
/// defaults if no persisted value exists. Writes are persisted immediately so they
/// survive application restarts.
/// </summary>
internal sealed class PersistentSecurityPolicyService : ISecurityPolicyService
{
    private const string PwKey  = "PasswordPolicy";
    private const string SecKey = "SecurityPolicy";

    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly IOptionsMonitor<AuthManagerOptions>     _monitor;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    // Runtime cache — avoids a DB round-trip on every read
    private PasswordPolicyOptions? _passwordCache;
    private SecurityPolicyOptions? _securityCache;

    public PersistentSecurityPolicyService(
        IDbContextFactory<AuthManagerDbContext> factory,
        IOptionsMonitor<AuthManagerOptions> monitor)
    {
        _factory = factory;
        _monitor = monitor;
    }

    public PasswordPolicyOptions GetPasswordPolicy()
    {
        if (_passwordCache is not null) return _passwordCache;

        using var db    = _factory.CreateDbContext();
        var row         = db.Settings.Find(PwKey);
        _passwordCache  = row is null
            ? Clone(_monitor.CurrentValue.PasswordPolicy)
            : JsonSerializer.Deserialize<PasswordPolicyOptions>(row.ValueJson, _json)
              ?? Clone(_monitor.CurrentValue.PasswordPolicy);
        return _passwordCache;
    }

    public async Task UpdatePasswordPolicyAsync(
        PasswordPolicyOptions policy, CancellationToken ct = default)
    {
        _passwordCache = policy;
        await UpsertAsync(PwKey, policy, ct);
    }

    public SecurityPolicyOptions GetSecurityPolicy()
    {
        if (_securityCache is not null) return _securityCache;

        using var db    = _factory.CreateDbContext();
        var row         = db.Settings.Find(SecKey);
        _securityCache  = row is null
            ? Clone(_monitor.CurrentValue.SecurityPolicy)
            : JsonSerializer.Deserialize<SecurityPolicyOptions>(row.ValueJson, _json)
              ?? Clone(_monitor.CurrentValue.SecurityPolicy);
        return _securityCache;
    }

    public async Task UpdateSecurityPolicyAsync(
        SecurityPolicyOptions policy, CancellationToken ct = default)
    {
        _securityCache = policy;
        await UpsertAsync(SecKey, policy, ct);
    }

    private async Task UpsertAsync<T>(string key, T value, CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Settings.FindAsync([key], ct);
        var json = JsonSerializer.Serialize(value, _json);

        if (row is null)
            db.Settings.Add(new AuthManagerSettingRecord { Key = key, ValueJson = json });
        else
        {
            row.ValueJson  = json;
            row.UpdatedAt  = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

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
