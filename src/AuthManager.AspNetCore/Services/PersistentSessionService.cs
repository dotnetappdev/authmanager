using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// SQLite-backed session tracker. Sessions persist across restarts so the
/// Active Sessions dashboard remains accurate after a process recycle.
/// </summary>
internal sealed class PersistentSessionService : ISessionService
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;

    public PersistentSessionService(IDbContextFactory<AuthManagerDbContext> factory)
        => _factory = factory;

    public async Task TrackSessionAsync(SessionInfo session, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var existing = await db.Sessions.FindAsync([session.SessionId], ct);
        if (existing is null)
        {
            db.Sessions.Add(Map(session));
        }
        else
        {
            existing.LastActiveAt    = session.LastActiveAt;
            existing.ExpiresAt       = session.ExpiresAt;
            existing.IpAddress       = session.IpAddress;
            existing.UserAgent       = session.UserAgent;
            existing.DeviceDescription = session.DeviceDescription;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SessionInfo>> GetUserSessionsAsync(
        string userId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.Sessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastActiveAt)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<SessionInfo>> GetAllSessionsAsync(
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.Sessions
            .OrderByDescending(s => s.LastActiveAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task RevokeSessionAsync(string sessionId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Sessions.FindAsync([sessionId], ct);
        if (row is not null)
        {
            db.Sessions.Remove(row);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeAllUserSessionsAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.Sessions.Where(s => s.UserId == userId).ToListAsync(ct);
        db.Sessions.RemoveRange(rows);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> GetActiveSessionCountAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Sessions.CountAsync(ct);
    }

    private static AuthManagerSessionRecord Map(SessionInfo s) => new()
    {
        SessionId         = s.SessionId,
        UserId            = s.UserId,
        UserName          = s.UserName,
        CreatedAt         = s.CreatedAt,
        LastActiveAt      = s.LastActiveAt,
        ExpiresAt         = s.ExpiresAt,
        IpAddress         = s.IpAddress,
        UserAgent         = s.UserAgent,
        DeviceDescription = s.DeviceDescription,
    };

    private static SessionInfo Map(AuthManagerSessionRecord r) => new()
    {
        SessionId         = r.SessionId,
        UserId            = r.UserId,
        UserName          = r.UserName,
        CreatedAt         = r.CreatedAt,
        LastActiveAt      = r.LastActiveAt,
        ExpiresAt         = r.ExpiresAt,
        IpAddress         = r.IpAddress,
        UserAgent         = r.UserAgent,
        DeviceDescription = r.DeviceDescription,
    };
}
