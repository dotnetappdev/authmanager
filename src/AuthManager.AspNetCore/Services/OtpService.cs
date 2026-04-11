using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Generates and verifies email-based one-time passwords (OTP).
/// Codes are stored as SHA-256 hashes in AuthManager's internal database.
/// </summary>
internal sealed class OtpService : IOtpService
{
    private const string OtpSettingsKey = "OtpSettings";

    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly IOptionsMonitor<AuthManagerOptions> _options;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        IDbContextFactory<AuthManagerDbContext> factory,
        IOptionsMonitor<AuthManagerOptions> options,
        ILogger<OtpService> logger)
    {
        _factory = factory;
        _options = options;
        _logger  = logger;
    }

    // ── Generate ─────────────────────────────────────────────────────────────

    public async Task<OtpGenerateResult> GenerateAsync(
        string userId, string purpose, CancellationToken ct = default)
    {
        var opts = await GetEffectiveOptionsAsync(ct);

        await using var db = await _factory.CreateDbContextAsync(ct);

        // Enforce resend cooldown — check for a non-expired code created recently
        var recent = await db.OtpCodes
            .Where(o => o.UserId == userId && o.Purpose == purpose
                     && !o.IsUsed && o.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (recent is not null)
        {
            var cooldownEnd = recent.CreatedAt.Add(opts.ResendCooldown);
            if (DateTimeOffset.UtcNow < cooldownEnd)
            {
                var wait = (int)(cooldownEnd - DateTimeOffset.UtcNow).TotalSeconds;
                return new OtpGenerateResult
                {
                    Success = false,
                    Error   = $"Please wait {wait} second(s) before requesting a new code."
                };
            }
        }

        // Generate plain-text code
        var plain = GeneratePlainCode(opts.CodeLength, opts.UseAlphanumericCodes);
        var hash  = HashCode(plain);
        var expiry = DateTimeOffset.UtcNow.Add(opts.CodeExpiry);

        db.OtpCodes.Add(new OtpRecord
        {
            UserId    = userId,
            Purpose   = purpose,
            CodeHash  = hash,
            ExpiresAt = expiry
        });
        await db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "[DotNetAuthManager] OTP generated for user {UserId}, purpose={Purpose}, expires={Expiry}.",
            userId, purpose, expiry);

        return new OtpGenerateResult
        {
            Success    = true,
            PlainCode  = plain,
            ExpiresAt  = expiry
        };
    }

    // ── Verify ───────────────────────────────────────────────────────────────

    public async Task<OtpVerifyResult> VerifyAsync(
        string userId, string purpose, string code, CancellationToken ct = default)
    {
        var opts = await GetEffectiveOptionsAsync(ct);
        var hash = HashCode(code.Trim());

        await using var db = await _factory.CreateDbContextAsync(ct);

        var record = await db.OtpCodes
            .Where(o => o.UserId == userId && o.Purpose == purpose && !o.IsUsed)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (record is null)
        {
            return new OtpVerifyResult { Success = false, Error = "No active code found." };
        }

        if (record.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return new OtpVerifyResult { Success = false, Error = "Code has expired." };
        }

        if (record.Attempts >= opts.MaxAttempts)
        {
            return new OtpVerifyResult { Success = false, Error = "Maximum attempts exceeded." };
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(record.CodeHash),
                Encoding.UTF8.GetBytes(hash)))
        {
            record.Attempts++;
            await db.SaveChangesAsync(ct);

            var remaining = Math.Max(0, opts.MaxAttempts - record.Attempts);
            return new OtpVerifyResult
            {
                Success          = false,
                Error            = "Invalid code.",
                AttemptsRemaining = remaining
            };
        }

        // Success — consume the code
        record.IsUsed = true;
        record.UsedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "[DotNetAuthManager] OTP verified for user {UserId}, purpose={Purpose}.",
            userId, purpose);

        return new OtpVerifyResult { Success = true };
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    public async Task<OtpSettingsInfo> GetSettingsAsync(CancellationToken ct = default)
    {
        var opts    = await GetEffectiveOptionsAsync(ct);
        var today   = DateTimeOffset.UtcNow.Date;
        var todayEnd = today.AddDays(1);

        await using var db = await _factory.CreateDbContextAsync(ct);

        var issuedToday   = await db.OtpCodes.CountAsync(o => o.CreatedAt >= today && o.CreatedAt < todayEnd, ct);
        var verifiedToday = await db.OtpCodes.CountAsync(o => o.IsUsed && o.UsedAt >= today && o.UsedAt < todayEnd, ct);
        var expiredToday  = await db.OtpCodes.CountAsync(o => !o.IsUsed && o.ExpiresAt >= today && o.ExpiresAt < todayEnd, ct);
        var pending       = await db.OtpCodes.CountAsync(o => !o.IsUsed && o.ExpiresAt > DateTimeOffset.UtcNow, ct);

        return new OtpSettingsInfo
        {
            Enabled              = opts.Enabled,
            CodeLength           = opts.CodeLength,
            CodeExpiryMinutes    = (int)opts.CodeExpiry.TotalMinutes,
            MaxAttempts          = opts.MaxAttempts,
            ResendCooldownSeconds = (int)opts.ResendCooldown.TotalSeconds,
            UseAlphanumericCodes = opts.UseAlphanumericCodes,
            EmailSubject         = opts.EmailSubject,
            EmailBodyTemplate    = opts.EmailBodyTemplate,
            TotalIssuedToday     = issuedToday,
            TotalVerifiedToday   = verifiedToday,
            TotalExpiredToday    = expiredToday,
            TotalPending         = pending
        };
    }

    public async Task<(bool Success, string[] Errors)> UpdateSettingsAsync(
        OtpSettingsInfo settings, CancellationToken ct = default)
    {
        if (settings.CodeLength is < 4 or > 12)
            return (false, ["Code length must be between 4 and 12."]);
        if (settings.CodeExpiryMinutes < 1)
            return (false, ["Code expiry must be at least 1 minute."]);
        if (settings.MaxAttempts < 1)
            return (false, ["Max attempts must be at least 1."]);

        await using var db = await _factory.CreateDbContextAsync(ct);

        var payload = JsonSerializer.Serialize(new
        {
            settings.Enabled,
            settings.CodeLength,
            settings.CodeExpiryMinutes,
            settings.MaxAttempts,
            settings.ResendCooldownSeconds,
            settings.UseAlphanumericCodes,
            settings.EmailSubject,
            settings.EmailBodyTemplate
        });

        await UpsertSettingAsync(db, OtpSettingsKey, payload, ct);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("[DotNetAuthManager] OTP settings updated.");
        return (true, []);
    }

    public async Task RevokeAllAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var active = await db.OtpCodes
            .Where(o => o.UserId == userId && !o.IsUsed && o.ExpiresAt > DateTimeOffset.UtcNow)
            .ToListAsync(ct);

        foreach (var r in active)
            r.ExpiresAt = DateTimeOffset.UtcNow; // expire immediately

        await db.SaveChangesAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<OtpOptions> GetEffectiveOptionsAsync(CancellationToken ct)
    {
        // Try loading persisted overrides from the settings store first
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Settings.FindAsync([OtpSettingsKey], ct);

        if (row is null) return _options.CurrentValue.Otp;

        try
        {
            using var doc = JsonDocument.Parse(row.ValueJson);
            var root = doc.RootElement;
            var fallback = _options.CurrentValue.Otp;

            return new OtpOptions
            {
                Enabled              = root.TryGetProperty("Enabled", out var en) && en.GetBoolean(),
                CodeLength           = root.TryGetProperty("CodeLength", out var cl) ? cl.GetInt32() : fallback.CodeLength,
                CodeExpiry           = root.TryGetProperty("CodeExpiryMinutes", out var ce)
                                           ? TimeSpan.FromMinutes(ce.GetInt32())
                                           : fallback.CodeExpiry,
                MaxAttempts          = root.TryGetProperty("MaxAttempts", out var ma) ? ma.GetInt32() : fallback.MaxAttempts,
                ResendCooldown       = root.TryGetProperty("ResendCooldownSeconds", out var rc)
                                           ? TimeSpan.FromSeconds(rc.GetInt32())
                                           : fallback.ResendCooldown,
                UseAlphanumericCodes = root.TryGetProperty("UseAlphanumericCodes", out var ua) && ua.GetBoolean(),
                EmailSubject         = root.TryGetProperty("EmailSubject", out var es)
                                           ? es.GetString() ?? fallback.EmailSubject
                                           : fallback.EmailSubject,
                EmailBodyTemplate    = root.TryGetProperty("EmailBodyTemplate", out var eb)
                                           ? eb.GetString() ?? fallback.EmailBodyTemplate
                                           : fallback.EmailBodyTemplate
            };
        }
        catch
        {
            return _options.CurrentValue.Otp;
        }
    }

    private static string GeneratePlainCode(int length, bool alphanumeric)
    {
        const string numericChars = "0123456789";
        const string alphaChars   = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // omit confusable chars

        var chars   = alphanumeric ? alphaChars : numericChars;
        var result  = new char[length];
        var buffer  = RandomNumberGenerator.GetBytes(length * 4);

        for (var i = 0; i < length; i++)
        {
            var idx = (int)(BitConverter.ToUInt32(buffer, i * 4) % (uint)chars.Length);
            result[i] = chars[idx];
        }
        return new string(result);
    }

    private static string HashCode(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static async Task UpsertSettingAsync(
        AuthManagerDbContext db, string key, string valueJson, CancellationToken ct)
    {
        var existing = await db.Settings.FindAsync([key], ct);
        if (existing is null)
            db.Settings.Add(new AuthManagerSettingRecord { Key = key, ValueJson = valueJson });
        else
        {
            existing.ValueJson = valueJson;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
