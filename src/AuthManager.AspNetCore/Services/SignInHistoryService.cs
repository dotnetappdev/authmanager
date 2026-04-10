using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// SQLite-backed sign-in history service. Singleton — uses IDbContextFactory to
/// avoid holding an open connection across requests.
/// </summary>
internal sealed class SignInHistoryService : ISignInHistoryService
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;

    public SignInHistoryService(IDbContextFactory<AuthManagerDbContext> factory)
        => _factory = factory;

    public async Task RecordAsync(SignInAttempt attempt, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.SignInAttempts.Add(new SignInAttemptRecord
        {
            UserId        = attempt.UserId,
            UserName      = attempt.UserName,
            Email         = attempt.Email,
            Succeeded     = attempt.Succeeded,
            FailureReason = attempt.FailureReason,
            IpAddress     = attempt.IpAddress,
            UserAgent     = attempt.UserAgent,
            Timestamp     = attempt.Timestamp,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<SignInAttempt>> GetHistoryAsync(
        int page,
        int pageSize,
        string? userId    = null,
        bool?   succeeded = null,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var query = db.SignInAttempts.AsQueryable();

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(x => x.UserId == userId);

        if (succeeded.HasValue)
            query = query.Where(x => x.Succeeded == succeeded.Value);

        var total = await query.CountAsync(ct);
        var rows  = await query
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<SignInAttempt>
        {
            Items      = rows.Select(Map).ToList(),
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    public async Task<int> GetRecentFailureCountAsync(
        string userId, TimeSpan window, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var since = DateTimeOffset.UtcNow - window;
        return await db.SignInAttempts
            .CountAsync(x => x.UserId == userId && !x.Succeeded && x.Timestamp > since, ct);
    }

    public async Task<int> GetTotalFailuresAsync(TimeSpan window, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var since = DateTimeOffset.UtcNow - window;
        return await db.SignInAttempts
            .CountAsync(x => !x.Succeeded && x.Timestamp > since, ct);
    }

    public async Task PurgeOldEntriesAsync(DateTimeOffset before, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.SignInAttempts
            .Where(x => x.Timestamp < before)
            .ExecuteDeleteAsync(ct);
    }

    private static SignInAttempt Map(SignInAttemptRecord r) => new()
    {
        Id            = r.Id,
        UserId        = r.UserId,
        UserName      = r.UserName,
        Email         = r.Email,
        Succeeded     = r.Succeeded,
        FailureReason = r.FailureReason,
        IpAddress     = r.IpAddress,
        UserAgent     = r.UserAgent,
        Timestamp     = r.Timestamp,
    };
}
