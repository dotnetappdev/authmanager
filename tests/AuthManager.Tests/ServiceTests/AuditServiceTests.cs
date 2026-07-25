using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

/// <summary>
/// Also regression coverage for the SQLite DateTimeOffset ORDER BY bug (see
/// <see cref="SignInHistoryServiceTests"/>) — the audit log's list and CSV export both
/// order by <c>Timestamp</c>.
/// </summary>
public sealed class AuditServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetAuditLogAsync_returns_entries_most_recent_first()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAuditService>();

        await svc.RecordAsync(new AuditEntry { Action = AuditActions.UserCreated, EntityType = "User", EntityId = "1" });
        await Task.Delay(10);
        await svc.RecordAsync(new AuditEntry { Action = AuditActions.UserUpdated, EntityType = "User", EntityId = "1" });

        var page = await svc.GetAuditLogAsync(1, 25);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(AuditActions.UserUpdated, page.Items[0].Action);
    }

    [Fact]
    public async Task ExportAuditLogCsvAsync_produces_a_header_plus_one_row_per_entry()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAuditService>();
        await svc.RecordAsync(new AuditEntry { Action = AuditActions.UserCreated, EntityType = "User", EntityId = "1", EntityName = "alice" });
        await svc.RecordAsync(new AuditEntry { Action = AuditActions.UserDeleted, EntityType = "User", EntityId = "2", EntityName = "bob" });

        var csvBytes = await svc.ExportAuditLogCsvAsync();
        var csv = System.Text.Encoding.UTF8.GetString(csvBytes);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length); // header + 2 rows
        Assert.Contains("alice", csv);
        Assert.Contains("bob", csv);
    }
}
