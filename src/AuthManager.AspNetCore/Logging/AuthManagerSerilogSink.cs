using AuthManager.Core.Models;
using AuthManager.AspNetCore.Services;
using Serilog.Core;
using Serilog.Events;
using Microsoft.Extensions.DependencyInjection;

namespace AuthManager.AspNetCore.Logging;

/// <summary>
/// A Serilog sink that feeds log events into the AuthManager log viewer.
/// </summary>
public sealed class AuthManagerSerilogSink : ILogEventSink
{
    private readonly IServiceProvider _serviceProvider;

    public AuthManagerSerilogSink(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var service = scope.ServiceProvider.GetService<LogAggregationService>();
            if (service is null) return;

            var entry = new LogEntry
            {
                Timestamp = logEvent.Timestamp,
                Level = MapLevel(logEvent.Level),
                Message = logEvent.MessageTemplate.Text,
                RenderedMessage = logEvent.RenderMessage(),
                Exception = logEvent.Exception?.ToString()
            };

            if (logEvent.Properties.TryGetValue("SourceContext", out var sc))
                entry.SourceContext = sc.ToString().Trim('"');

            if (logEvent.Properties.TryGetValue("RequestId", out var rid))
                entry.RequestId = rid.ToString().Trim('"');

            if (logEvent.Properties.TryGetValue("RequestPath", out var rp))
                entry.RequestPath = rp.ToString().Trim('"');

            if (logEvent.Properties.TryGetValue("TraceId", out var ti))
                entry.TraceId = ti.ToString().Trim('"');

            foreach (var prop in logEvent.Properties)
            {
                if (prop.Key is "SourceContext" or "RequestId" or "RequestPath" or "TraceId")
                    continue;
                entry.Properties[prop.Key] = prop.Value.ToString();
            }

            service.AddEntry(entry);
        }
        catch
        {
            // Never throw from a sink
        }
    }

    private static LogLevel MapLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => LogLevel.Verbose,
        LogEventLevel.Debug => LogLevel.Debug,
        LogEventLevel.Information => LogLevel.Information,
        LogEventLevel.Warning => LogLevel.Warning,
        LogEventLevel.Error => LogLevel.Error,
        LogEventLevel.Fatal => LogLevel.Fatal,
        _ => LogLevel.Information
    };
}
