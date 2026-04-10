namespace AuthManager.Core.Services;

/// <summary>
/// Dispatches signed HTTP webhook events to configured endpoints.
/// Use <see cref="WebhookEventNames"/> for well-known event names.
///
/// Implementations must be resilient (retry, timeout) — the caller
/// should not block on webhook delivery.
/// </summary>
public interface IWebhookService
{
    /// <summary>
    /// Fire an event to all endpoints subscribed to <paramref name="eventName"/>.
    /// This is fire-and-forget — failures are logged but not thrown.
    /// </summary>
    Task DispatchAsync(
        string eventName,
        string? userId  = null,
        string? email   = null,
        object? payload = null,
        CancellationToken ct = default);
}
