using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AuthManager.Core.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Manages display names used throughout the AuthManager UI.
/// Values are persisted in the internal database and override startup defaults.
/// </summary>
internal sealed class EntityNamingService : IEntityNamingService
{
    private const string Key = "EntityNaming";

    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly IOptionsMonitor<AuthManagerOptions> _monitor;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    // In-memory cache
    private string? _singular;
    private string? _plural;

    public EntityNamingService(
        IDbContextFactory<AuthManagerDbContext> factory,
        IOptionsMonitor<AuthManagerOptions> monitor)
    {
        _factory = factory;
        _monitor = monitor;
    }

    public string GetUserDisplayName()
    {
        if (_singular is not null) return _singular;
        Load();
        return _singular!;
    }

    public string GetUsersDisplayName()
    {
        if (_plural is not null) return _plural;
        Load();
        return _plural!;
    }

    public async Task SaveNamingAsync(
        string singular, string plural, CancellationToken ct = default)
    {
        _singular = singular;
        _plural   = plural;

        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Settings.FindAsync([Key], ct);
        var json = JsonSerializer.Serialize(new { singular, plural }, _json);

        if (row is null)
            db.Settings.Add(new AuthManagerSettingRecord { Key = Key, ValueJson = json });
        else
        {
            row.ValueJson  = json;
            row.UpdatedAt  = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private void Load()
    {
        try
        {
            using var db = _factory.CreateDbContext();
            var row  = db.Settings.Find(Key);
            if (row is not null)
            {
                var obj = JsonSerializer.Deserialize<NamingRecord>(row.ValueJson, _json);
                if (obj is not null)
                {
                    _singular = obj.Singular;
                    _plural   = obj.Plural;
                    return;
                }
            }
        }
        catch { /* fall through to defaults */ }

        var opts  = _monitor.CurrentValue;
        _singular = string.IsNullOrWhiteSpace(opts.UserEntityDisplayName)
            ? "User" : opts.UserEntityDisplayName;
        _plural   = string.IsNullOrWhiteSpace(opts.UserEntityPluralDisplayName)
            ? "Users" : opts.UserEntityPluralDisplayName;
    }

    private sealed record NamingRecord(string Singular, string Plural);
}
