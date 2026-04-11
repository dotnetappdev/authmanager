using AuthManager.AspNetCore.Data;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// SQLite-backed key-value settings store. Uses <see cref="AuthManagerSettingRecord"/>
/// so all persisted settings live in the same internal database as policies and audit data.
/// </summary>
internal sealed class SettingsStore : ISettingsStore
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;

    public SettingsStore(IDbContextFactory<AuthManagerDbContext> factory)
        => _factory = factory;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return (await db.Settings.FindAsync([key], ct))?.ValueJson;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var existing = await db.Settings.FindAsync([key], ct);
        if (existing is null)
            db.Settings.Add(new AuthManagerSettingRecord { Key = key, ValueJson = value });
        else
            existing.ValueJson = value;
        await db.SaveChangesAsync(ct);
    }

    public async Task<Dictionary<string, string>> GetByPrefixAsync(
        string prefix, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Settings
            .Where(s => s.Key.StartsWith(prefix))
            .ToDictionaryAsync(s => s.Key, s => s.ValueJson, ct);
    }
}
