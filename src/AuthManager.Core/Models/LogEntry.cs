namespace AuthManager.Core.Models;

/// <summary>
/// Represents a single log entry captured from Serilog.
/// </summary>
public sealed class LogEntry
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? RenderedMessage { get; set; }
    public string? Exception { get; set; }
    public string? SourceContext { get; set; }
    public string? RequestId { get; set; }
    public string? RequestPath { get; set; }
    public string? TraceId { get; set; }
    public Dictionary<string, string?> Properties { get; set; } = [];
}

public enum LogLevel
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal
}

/// <summary>
/// Filter parameters for log queries.
/// </summary>
public sealed class LogFilter
{
    public string? SearchTerm { get; set; }
    public LogLevel? MinLevel { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? SourceContext { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}
