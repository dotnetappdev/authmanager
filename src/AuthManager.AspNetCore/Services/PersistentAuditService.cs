using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// SQLite-backed audit service. Persists audit entries across restarts.
/// Replaces the in-memory implementation when AuthManager's internal DB is configured.
/// </summary>
internal sealed class PersistentAuditService : IAuditService
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public PersistentAuditService(IDbContextFactory<AuthManagerDbContext> factory)
        => _factory = factory;

    public async Task RecordAsync(AuditEntry entry, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        db.AuditEntries.Add(new AuditEntryRecord
        {
            Timestamp            = entry.Timestamp,
            Action               = entry.Action,
            EntityType           = entry.EntityType,
            EntityId             = entry.EntityId,
            EntityName           = entry.EntityName,
            PerformedByUserId    = entry.PerformedByUserId,
            PerformedByUserName  = entry.PerformedByUserName,
            IpAddress            = entry.IpAddress,
            OldValuesJson        = entry.OldValues.Count > 0
                                       ? JsonSerializer.Serialize(entry.OldValues, _json)
                                       : null,
            NewValuesJson        = entry.NewValues.Count > 0
                                       ? JsonSerializer.Serialize(entry.NewValues, _json)
                                       : null,
            Success              = entry.Success,
            ErrorMessage         = entry.ErrorMessage,
        });

        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditEntry>> GetAuditLogAsync(
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var total = await db.AuditEntries.CountAsync(ct);
        var rows  = await db.AuditEntries
            .OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditEntry>
        {
            Items      = rows.Select(Map).ToList(),
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    private static AuditEntry Map(AuditEntryRecord r) => new()
    {
        Id                  = r.Id,
        Timestamp           = r.Timestamp,
        Action              = r.Action,
        EntityType          = r.EntityType,
        EntityId            = r.EntityId,
        EntityName          = r.EntityName,
        PerformedByUserId   = r.PerformedByUserId,
        PerformedByUserName = r.PerformedByUserName,
        IpAddress           = r.IpAddress,
        OldValues           = r.OldValuesJson is null
                                  ? []
                                  : JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                        r.OldValuesJson, _json) ?? [],
        NewValues           = r.NewValuesJson is null
                                  ? []
                                  : JsonSerializer.Deserialize<Dictionary<string, object?>>(
                                        r.NewValuesJson, _json) ?? [],
        Success             = r.Success,
        ErrorMessage        = r.ErrorMessage,
    };
}
