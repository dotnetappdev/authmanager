using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

public interface ISignInHistoryService
{
    /// <summary>Record a sign-in attempt (call from your login handler).</summary>
    Task RecordAsync(SignInAttempt attempt, CancellationToken ct = default);

    Task<PagedResult<SignInAttempt>> GetHistoryAsync(
        int page,
        int pageSize,
        string? userId = null,
        bool? succeeded = null,
        CancellationToken ct = default);

    Task<int> GetRecentFailureCountAsync(string userId, TimeSpan window, CancellationToken ct = default);

    Task<int> GetTotalFailuresAsync(TimeSpan window, CancellationToken ct = default);

    Task PurgeOldEntriesAsync(DateTimeOffset before, CancellationToken ct = default);
}
