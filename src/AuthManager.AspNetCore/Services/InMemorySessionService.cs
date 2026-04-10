using System.Collections.Concurrent;
using AuthManager.Core.Models;
using AuthManager.Core.Services;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// In-memory session tracker. Suitable for single-node deployments.
///
/// For distributed deployments replace with a Redis-backed implementation by
/// registering <c>ISessionService</c> after <c>AddAuthManager()</c>:
/// <code>
/// services.AddSingleton&lt;ISessionService, RedisSessionService&gt;();
/// </code>
/// </summary>
public sealed class InMemorySessionService : ISessionService
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();

    public Task TrackSessionAsync(SessionInfo session, CancellationToken ct = default)
    {
        _sessions[session.SessionId] = session;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SessionInfo>> GetUserSessionsAsync(
        string userId, CancellationToken ct = default)
    {
        var result = _sessions.Values
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastActiveAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<SessionInfo>>(result);
    }

    public Task<IReadOnlyList<SessionInfo>> GetAllSessionsAsync(
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var result = _sessions.Values
            .OrderByDescending(s => s.LastActiveAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<SessionInfo>>(result);
    }

    public Task RevokeSessionAsync(string sessionId, CancellationToken ct = default)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    public Task RevokeAllUserSessionsAsync(string userId, CancellationToken ct = default)
    {
        var keys = _sessions
            .Where(kv => kv.Value.UserId == userId)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in keys)
            _sessions.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    public Task<int> GetActiveSessionCountAsync(CancellationToken ct = default)
        => Task.FromResult(_sessions.Count);
}
