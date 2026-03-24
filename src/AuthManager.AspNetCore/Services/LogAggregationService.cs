using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.Extensions.Options;
using AuthManager.Core.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// In-memory log aggregation service backed by a Serilog sink.
/// </summary>
public sealed class LogAggregationService : ILogAggregationService
{
    private readonly ConcurrentQueue<LogEntry> _logs = new();
    private readonly LogViewerOptions _options;
    private long _idCounter;

    public LogAggregationService(IOptions<AuthManagerOptions> options)
    {
        _options = options.Value.LogViewer;
    }

    public void AddEntry(LogEntry entry)
    {
        entry.Id = System.Threading.Interlocked.Increment(ref _idCounter);

        _logs.Enqueue(entry);

        while (_logs.Count > _options.MaxLogEntries)
            _logs.TryDequeue(out _);
    }

    public Task<PagedResult<LogEntry>> GetLogsAsync(LogFilter filter, CancellationToken ct = default)
    {
        var query = _logs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.ToLowerInvariant();
            query = query.Where(l =>
                l.Message.ToLowerInvariant().Contains(term) ||
                (l.Exception != null && l.Exception.ToLowerInvariant().Contains(term)) ||
                (l.SourceContext != null && l.SourceContext.ToLowerInvariant().Contains(term)));
        }

        if (filter.MinLevel.HasValue)
            query = query.Where(l => l.Level >= filter.MinLevel.Value);

        if (filter.From.HasValue)
            query = query.Where(l => l.Timestamp >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(l => l.Timestamp <= filter.To.Value);

        if (!string.IsNullOrEmpty(filter.SourceContext))
            query = query.Where(l => l.SourceContext == filter.SourceContext);

        var ordered = query.OrderByDescending(l => l.Timestamp).ToList();
        var totalCount = ordered.Count;
        var items = ordered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return Task.FromResult(new PagedResult<LogEntry>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        });
    }

    public Task<List<string>> GetSourceContextsAsync(CancellationToken ct = default)
    {
        var contexts = _logs
            .Where(l => l.SourceContext != null)
            .Select(l => l.SourceContext!)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        return Task.FromResult(contexts);
    }

    public Task ClearLogsAsync(CancellationToken ct = default)
    {
        while (_logs.TryDequeue(out _)) { }
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<LogEntry> StreamLogsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        long lastId = 0;
        while (!ct.IsCancellationRequested)
        {
            var newEntries = _logs.Where(l => l.Id > lastId).OrderBy(l => l.Id).ToList();
            foreach (var entry in newEntries)
            {
                lastId = entry.Id;
                yield return entry;
            }

            await Task.Delay(_options.LiveUpdateIntervalMs, ct).ConfigureAwait(false);
        }
    }
}
