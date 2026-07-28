using System.Text;
using System.Text.Json;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.Extensions.Logging;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Sends SMS messages via whichever provider is active in <see cref="SmsOptions"/>, calling
/// each provider's REST API directly (no SDK dependency) through the named
/// "AuthManager.Sms" <see cref="IHttpClientFactory"/> client.
/// </summary>
internal sealed class SmsSenderService : ISmsSenderService
{
    private readonly ISmsSettingsService _settings;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<SmsSenderService> _logger;

    public SmsSenderService(ISmsSettingsService settings, IHttpClientFactory httpFactory, ILogger<SmsSenderService> logger)
    {
        _settings = settings;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<(bool Success, string[] Errors)> SendAsync(string toPhoneNumber, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toPhoneNumber))
            return (false, ["A destination phone number is required."]);

        var options = await _settings.GetRawSettingsAsync(ct);
        if (!options.Enabled)
            return (false, ["SMS delivery is not enabled. Configure it under OTP Settings first."]);

        return options.ActiveProvider switch
        {
            SmsProvider.Twilio      => await SendViaTwilioAsync(options.Twilio, toPhoneNumber, message, ct),
            SmsProvider.Vonage      => await SendViaVonageAsync(options.Vonage, toPhoneNumber, message, ct),
            SmsProvider.MessageBird => await SendViaMessageBirdAsync(options.MessageBird, toPhoneNumber, message, ct),
            SmsProvider.Sinch       => await SendViaSinchAsync(options.Sinch, toPhoneNumber, message, ct),
            SmsProvider.Textlocal   => await SendViaTextlocalAsync(options.Textlocal, toPhoneNumber, message, ct),
            _ => (false, ["No SMS provider is selected. Choose one under OTP Settings."]),
        };
    }

    private async Task<(bool, string[])> SendViaTwilioAsync(TwilioSmsOptions o, string to, string message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(o.AccountSid) || string.IsNullOrEmpty(o.AuthToken))
            return (false, ["Twilio Account SID/Auth Token is not configured."]);

        var client = _httpFactory.CreateClient("AuthManager.Sms");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{o.AccountSid}/Messages.json");
        req.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{o.AccountSid}:{o.AuthToken}")));

        var form = new Dictionary<string, string> { ["To"] = to, ["Body"] = message };
        form[o.FromNumber.StartsWith("MG", StringComparison.OrdinalIgnoreCase) ? "MessagingServiceSid" : "From"] = o.FromNumber;
        req.Content = new FormUrlEncodedContent(form);

        return await SendAndInterpretAsync(client, req, ct, body =>
            JsonDocument.Parse(body).RootElement.TryGetProperty("message", out var m) ? m.GetString() : null);
    }

    private async Task<(bool, string[])> SendViaVonageAsync(VonageSmsOptions o, string to, string message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(o.ApiKey) || string.IsNullOrEmpty(o.ApiSecret))
            return (false, ["Vonage API key/secret is not configured."]);

        var client = _httpFactory.CreateClient("AuthManager.Sms");
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://rest.nexmo.com/sms/json");
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["api_key"] = o.ApiKey,
            ["api_secret"] = o.ApiSecret,
            ["to"] = to.TrimStart('+'),
            ["from"] = o.FromNumber,
            ["text"] = message,
        });

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            return (false, [ExtractApiError(body, "Vonage rejected the SMS request.")]);

        // Vonage returns 200 even for per-message failures — check each message's own status.
        using var json = JsonDocument.Parse(body);
        var messages = json.RootElement.TryGetProperty("messages", out var m) ? m : default;
        if (messages.ValueKind == JsonValueKind.Array && messages.GetArrayLength() > 0)
        {
            var first = messages[0];
            var status = first.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (status != "0")
            {
                var errText = first.TryGetProperty("error-text", out var et) ? et.GetString() : "Vonage rejected the message.";
                return (false, [errText ?? "Vonage rejected the message."]);
            }
        }
        return (true, []);
    }

    private async Task<(bool, string[])> SendViaMessageBirdAsync(MessageBirdSmsOptions o, string to, string message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(o.ApiKey))
            return (false, ["MessageBird API key is not configured."]);

        var client = _httpFactory.CreateClient("AuthManager.Sms");
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://rest.messagebird.com/messages");
        req.Headers.Authorization = new("AccessKey", o.ApiKey);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["recipients"] = to,
            ["originator"] = o.Originator,
            ["body"] = message,
        });

        return await SendAndInterpretAsync(client, req, ct, body =>
        {
            using var json = JsonDocument.Parse(body);
            if (!json.RootElement.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array || errors.GetArrayLength() == 0)
                return null;
            return errors[0].TryGetProperty("description", out var d) ? d.GetString() : null;
        });
    }

    private async Task<(bool, string[])> SendViaSinchAsync(SinchSmsOptions o, string to, string message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(o.ServicePlanId) || string.IsNullOrEmpty(o.ApiToken))
            return (false, ["Sinch Service Plan ID/API token is not configured."]);

        var client = _httpFactory.CreateClient("AuthManager.Sms");
        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://sms.api.sinch.com/xms/v1/{o.ServicePlanId}/batches");
        req.Headers.Authorization = new("Bearer", o.ApiToken);
        req.Content = new StringContent(JsonSerializer.Serialize(new
        {
            from = o.FromNumber,
            to = new[] { to },
            body = message,
        }), Encoding.UTF8, "application/json");

        return await SendAndInterpretAsync(client, req, ct, body =>
            JsonDocument.Parse(body).RootElement.TryGetProperty("text", out var t) ? t.GetString() : null);
    }

    private async Task<(bool, string[])> SendViaTextlocalAsync(TextlocalSmsOptions o, string to, string message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(o.ApiKey))
            return (false, ["Textlocal API key is not configured."]);

        var client = _httpFactory.CreateClient("AuthManager.Sms");
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.txtlocal.com/send/");
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["apikey"] = o.ApiKey,
            ["numbers"] = to.TrimStart('+'),
            ["message"] = message,
            ["sender"] = o.Sender,
        });

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            return (false, [ExtractApiError(body, "Textlocal rejected the SMS request.")]);

        using var json = JsonDocument.Parse(body);
        var status = json.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
        if (status != "success")
        {
            var errText = json.RootElement.TryGetProperty("errors", out var errs) && errs.ValueKind == JsonValueKind.Array && errs.GetArrayLength() > 0
                ? errs[0].TryGetProperty("message", out var m) ? m.GetString() : null
                : null;
            return (false, [errText ?? "Textlocal rejected the message."]);
        }
        return (true, []);
    }

    private async Task<(bool, string[])> SendAndInterpretAsync(
        HttpClient client, HttpRequestMessage req, CancellationToken ct, Func<string, string?> extractError)
    {
        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (resp.IsSuccessStatusCode) return (true, []);

        string? errMessage = null;
        try { errMessage = extractError(body); } catch (JsonException) { /* not JSON */ }
        _logger.LogWarning("SMS provider request failed: {Status} {Body}", resp.StatusCode, body);
        return (false, [errMessage ?? $"The SMS provider rejected the request ({(int)resp.StatusCode})."]);
    }

    private static string ExtractApiError(string responseBody, string fallback)
    {
        try
        {
            using var json = JsonDocument.Parse(responseBody);
            if (json.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString() ?? fallback;
            if (json.RootElement.TryGetProperty("description", out var desc))
                return desc.GetString() ?? fallback;
        }
        catch (JsonException) { /* not JSON — fall through */ }
        return fallback;
    }
}
