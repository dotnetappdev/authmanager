using System.Collections.Concurrent;
using AuthManager.Core.Models;
using AuthManager.Core.Services;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// In-memory audit service. For production use, replace with a database-backed implementation.
/// </summary>
internal sealed class InMemoryAuditService : IAuditService
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();
    private long _idCounter;

    public Task RecordAsync(AuditEntry entry, CancellationToken ct = default)
    {
        entry.Id = System.Threading.Interlocked.Increment(ref _idCounter);
        _entries.Enqueue(entry);

        // Keep last 10,000 entries
        while (_entries.Count > 10_000)
            _entries.TryDequeue(out _);

        return Task.CompletedTask;
    }

    public Task<PagedResult<AuditEntry>> GetAuditLogAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var ordered = _entries.OrderByDescending(e => e.Timestamp).ToList();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new PagedResult<AuditEntry>
        {
            Items = items,
            TotalCount = ordered.Count,
            Page = page,
            PageSize = pageSize
        });
    }
}
