using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// SQLite-backed branding settings service. Mirrors <see cref="PersistentPaymentSettingsService"/>
/// minus the secret-masking (nothing in <see cref="BrandingOptions"/> is a secret).
/// </summary>
internal sealed class PersistentBrandingSettingsService : IBrandingSettingsService
{
    private const string Key = "BrandingOptions";

    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly IOptionsMonitor<AuthManagerOptions>     _monitor;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private BrandingOptions? _cache;

    public PersistentBrandingSettingsService(
        IDbContextFactory<AuthManagerDbContext> factory,
        IOptionsMonitor<AuthManagerOptions> monitor)
    {
        _factory = factory;
        _monitor = monitor;
    }

    public async Task<BrandingOptions> GetSettingsAsync(CancellationToken ct = default)
    {
        if (_cache is not null) return _cache;

        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Settings.FindAsync([Key], ct);
        _cache = row is null
            ? _monitor.CurrentValue.Branding
            : JsonSerializer.Deserialize<BrandingOptions>(row.ValueJson, _json) ?? _monitor.CurrentValue.Branding;
        return _cache;
    }

    public async Task UpdateSettingsAsync(BrandingOptions settings, CancellationToken ct = default)
    {
        _cache = settings;

        await using var db = await _factory.CreateDbContextAsync(ct);
        var row  = await db.Settings.FindAsync([Key], ct);
        var json = JsonSerializer.Serialize(settings, _json);

        if (row is null)
            db.Settings.Add(new AuthManagerSettingRecord { Key = Key, ValueJson = json });
        else
        {
            row.ValueJson = json;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
