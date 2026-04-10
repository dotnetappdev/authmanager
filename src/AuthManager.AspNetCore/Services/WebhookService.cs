using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Dispatches signed HTTP POST events to all enabled webhook endpoints
/// that subscribe to the fired event name.
/// </summary>
public sealed class WebhookService : IWebhookService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptions<AuthManagerOptions> _options;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        IHttpClientFactory httpFactory,
        IOptions<AuthManagerOptions> options,
        ILogger<WebhookService> logger)
    {
        _httpFactory = httpFactory;
        _options     = options;
        _logger      = logger;
    }

    public async Task DispatchAsync(
        string eventName,
        string? userId  = null,
        string? email   = null,
        object? payload = null,
        CancellationToken ct = default)
    {
        var cfg = _options.Value.Webhooks;
        if (!cfg.Enabled) return;

        var endpoints = cfg.Endpoints
            .Where(e => e.Enabled && SubscribesTo(e, eventName))
            .ToList();

        if (endpoints.Count == 0) return;

        var envelope = new
        {
            @event    = eventName,
            timestamp = DateTimeOffset.UtcNow,
            userId,
            email,
            data = payload
        };

        var json    = JsonSerializer.Serialize(envelope);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        var tasks = endpoints.Select(ep => DeliverAsync(ep, jsonBytes, cfg, ct));
        await Task.WhenAll(tasks);
    }

    private async Task DeliverAsync(
        WebhookEndpoint endpoint,
        byte[] body,
        WebhookOptions cfg,
        CancellationToken ct)
    {
        var sig = ComputeSignature(body, endpoint.Secret);

        for (int attempt = 0; attempt <= cfg.MaxRetries; attempt++)
        {
            try
            {
                using var client  = _httpFactory.CreateClient("AuthManager.Webhooks");
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
                {
                    Content = new ByteArrayContent(body)
                };
                request.Content.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Headers.TryAddWithoutValidation("X-AuthManager-Signature", sig);
                request.Headers.TryAddWithoutValidation("X-AuthManager-Attempt",  attempt.ToString());

                using var cts    = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(cfg.DeliveryTimeout);

                var response = await client.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug(
                        "[DotNetAuthManager] Webhook '{Endpoint}' delivered (attempt {Attempt}).",
                        endpoint.Name, attempt + 1);
                    return;
                }

                _logger.LogWarning(
                    "[DotNetAuthManager] Webhook '{Endpoint}' returned {Status} (attempt {Attempt}).",
                    endpoint.Name, (int)response.StatusCode, attempt + 1);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "[DotNetAuthManager] Webhook '{Endpoint}' delivery failed (attempt {Attempt}).",
                    endpoint.Name, attempt + 1);
            }

            if (attempt < cfg.MaxRetries)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }

        _logger.LogError(
            "[DotNetAuthManager] Webhook '{Endpoint}' permanently failed after {Retries} attempts.",
            endpoint.Name, cfg.MaxRetries + 1);
    }

    private static bool SubscribesTo(WebhookEndpoint endpoint, string eventName)
        => endpoint.Events.Contains(WebhookEventNames.All) ||
           endpoint.Events.Contains(eventName);

    private static string ComputeSignature(byte[] body, string secret)
    {
        if (string.IsNullOrEmpty(secret)) return string.Empty;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(body)).ToLowerInvariant();
    }
}
