using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

/// <summary>
/// Regression coverage for a systemic bug: SQLite's EF Core provider refuses to translate
/// ORDER BY (or relational &gt;/&lt; comparisons) on DateTimeOffset columns, which this
/// service's queries are built on. It silently broke every "list sorted by recency" read
/// path using the default SQLite internal store (Audit Log, Sessions, Sign-in History, API
/// Tokens, OTP lookups, System Health) until AuthManagerDbContext started mapping
/// DateTimeOffset via a global DateTimeOffsetToBinaryConverter. These tests exist so that
/// fix can never silently regress.
/// </summary>
public sealed class SignInHistoryServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetHistoryAsync_does_not_throw_when_ordering_by_timestamp()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISignInHistoryService>();

        await svc.RecordAsync(new SignInAttempt { UserId = "u1", UserName = "alice", Succeeded = true });
        await svc.RecordAsync(new SignInAttempt { UserId = "u1", UserName = "alice", Succeeded = false, FailureReason = "InvalidPassword" });

        var page = await svc.GetHistoryAsync(1, 25);

        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task GetTotalFailuresAsync_counts_only_recent_failures()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISignInHistoryService>();

        await svc.RecordAsync(new SignInAttempt { UserId = "u1", UserName = "alice", Succeeded = false });
        await svc.RecordAsync(new SignInAttempt { UserId = "u2", UserName = "bob", Succeeded = false });
        await svc.RecordAsync(new SignInAttempt { UserId = "u3", UserName = "carol", Succeeded = true });

        var failuresLastHour = await svc.GetTotalFailuresAsync(TimeSpan.FromHours(1));
        var failuresLastNanosecond = await svc.GetTotalFailuresAsync(TimeSpan.FromTicks(1));

        Assert.Equal(2, failuresLastHour);
        Assert.Equal(0, failuresLastNanosecond);
    }

    [Fact]
    public async Task GetRecentFailureCountAsync_scopes_to_a_single_user()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISignInHistoryService>();

        await svc.RecordAsync(new SignInAttempt { UserId = "u1", UserName = "alice", Succeeded = false });
        await svc.RecordAsync(new SignInAttempt { UserId = "u1", UserName = "alice", Succeeded = false });
        await svc.RecordAsync(new SignInAttempt { UserId = "u2", UserName = "bob", Succeeded = false });

        var aliceFailures = await svc.GetRecentFailureCountAsync("u1", TimeSpan.FromMinutes(15));
        var bobFailures = await svc.GetRecentFailureCountAsync("u2", TimeSpan.FromMinutes(15));

        Assert.Equal(2, aliceFailures);
        Assert.Equal(1, bobFailures);
    }

    [Fact]
    public async Task PurgeOldEntriesAsync_removes_only_entries_before_the_cutoff()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISignInHistoryService>();
        await svc.RecordAsync(new SignInAttempt { UserId = "u1", UserName = "alice", Succeeded = true });

        await svc.PurgeOldEntriesAsync(DateTimeOffset.UtcNow.AddDays(-1));
        var stillThere = await svc.GetHistoryAsync(1, 25);

        await svc.PurgeOldEntriesAsync(DateTimeOffset.UtcNow.AddDays(1));
        var purged = await svc.GetHistoryAsync(1, 25);

        Assert.Equal(1, stillThere.TotalCount);
        Assert.Equal(0, purged.TotalCount);
    }
}
