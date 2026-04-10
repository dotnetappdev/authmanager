using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Tracks and manages active user sessions.
/// Equivalent to Keycloak's "Sessions" admin panel.
/// </summary>
public interface ISessionService
{
    /// <summary>Get all active sessions for a specific user.</summary>
    Task<IReadOnlyList<SessionInfo>> GetUserSessionsAsync(string userId, CancellationToken ct = default);

    /// <summary>Get a paged view of all active sessions across all users.</summary>
    Task<IReadOnlyList<SessionInfo>> GetAllSessionsAsync(int page = 1, int pageSize = 50, CancellationToken ct = default);

    /// <summary>
    /// Terminate a specific session. Forces re-authentication on the next request.
    /// </summary>
    Task RevokeSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>Terminate ALL sessions for a user (sign out all devices).</summary>
    Task RevokeAllUserSessionsAsync(string userId, CancellationToken ct = default);

    /// <summary>Total number of active sessions across all users.</summary>
    Task<int> GetActiveSessionCountAsync(CancellationToken ct = default);

    /// <summary>
    /// Record a new or refreshed session. Called by the auth pipeline.
    /// </summary>
    Task TrackSessionAsync(SessionInfo session, CancellationToken ct = default);
}
