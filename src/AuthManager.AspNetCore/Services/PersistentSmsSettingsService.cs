using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// SQLite-backed SMS provider settings service. Mirrors
/// <see cref="PersistentPaymentSettingsService"/>: reads/writes a JSON-serialized
/// <see cref="SmsOptions"/> under a single settings key, falling back to
/// <see cref="AuthManagerOptions"/> defaults when nothing has been persisted yet.
/// </summary>
internal sealed class PersistentSmsSettingsService : ISmsSettingsService
{
    private const string Key = "SmsOptions";

    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly IOptionsMonitor<AuthManagerOptions>     _monitor;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private SmsOptions? _cache;

    public PersistentSmsSettingsService(
        IDbContextFactory<AuthManagerDbContext> factory,
        IOptionsMonitor<AuthManagerOptions> monitor)
    {
        _factory = factory;
        _monitor = monitor;
    }

    public async Task<SmsSettingsInfo> GetSettingsAsync(CancellationToken ct = default)
    {
        var o = await GetOptionsAsync(ct);
        return new SmsSettingsInfo
        {
            Enabled = o.Enabled,
            ActiveProvider = o.ActiveProvider,

            TwilioAccountSid = o.Twilio.AccountSid,
            TwilioAuthTokenSet = !string.IsNullOrEmpty(o.Twilio.AuthToken),
            TwilioAuthTokenMasked = Mask(o.Twilio.AuthToken),
            TwilioFromNumber = o.Twilio.FromNumber,

            VonageApiKey = o.Vonage.ApiKey,
            VonageApiSecretSet = !string.IsNullOrEmpty(o.Vonage.ApiSecret),
            VonageApiSecretMasked = Mask(o.Vonage.ApiSecret),
            VonageFromNumber = o.Vonage.FromNumber,

            MessageBirdApiKeySet = !string.IsNullOrEmpty(o.MessageBird.ApiKey),
            MessageBirdApiKeyMasked = Mask(o.MessageBird.ApiKey),
            MessageBirdOriginator = o.MessageBird.Originator,

            SinchServicePlanId = o.Sinch.ServicePlanId,
            SinchApiTokenSet = !string.IsNullOrEmpty(o.Sinch.ApiToken),
            SinchApiTokenMasked = Mask(o.Sinch.ApiToken),
            SinchFromNumber = o.Sinch.FromNumber,

            TextlocalApiKeySet = !string.IsNullOrEmpty(o.Textlocal.ApiKey),
            TextlocalApiKeyMasked = Mask(o.Textlocal.ApiKey),
            TextlocalSender = o.Textlocal.Sender,
        };
    }

    public async Task UpdateSettingsAsync(UpdateSmsSettingsDto dto, CancellationToken ct = default)
    {
        var current = await GetOptionsAsync(ct);

        var updated = new SmsOptions
        {
            Enabled = dto.Enabled,
            ActiveProvider = dto.ActiveProvider,
            Twilio = new TwilioSmsOptions
            {
                AccountSid = dto.TwilioAccountSid,
                AuthToken = string.IsNullOrEmpty(dto.TwilioAuthToken) ? current.Twilio.AuthToken : dto.TwilioAuthToken,
                FromNumber = dto.TwilioFromNumber,
            },
            Vonage = new VonageSmsOptions
            {
                ApiKey = dto.VonageApiKey,
                ApiSecret = string.IsNullOrEmpty(dto.VonageApiSecret) ? current.Vonage.ApiSecret : dto.VonageApiSecret,
                FromNumber = dto.VonageFromNumber,
            },
            MessageBird = new MessageBirdSmsOptions
            {
                ApiKey = string.IsNullOrEmpty(dto.MessageBirdApiKey) ? current.MessageBird.ApiKey : dto.MessageBirdApiKey,
                Originator = dto.MessageBirdOriginator,
            },
            Sinch = new SinchSmsOptions
            {
                ServicePlanId = dto.SinchServicePlanId,
                ApiToken = string.IsNullOrEmpty(dto.SinchApiToken) ? current.Sinch.ApiToken : dto.SinchApiToken,
                FromNumber = dto.SinchFromNumber,
            },
            Textlocal = new TextlocalSmsOptions
            {
                ApiKey = string.IsNullOrEmpty(dto.TextlocalApiKey) ? current.Textlocal.ApiKey : dto.TextlocalApiKey,
                Sender = dto.TextlocalSender,
            },
        };

        _cache = updated;

        await using var db = await _factory.CreateDbContextAsync(ct);
        var row  = await db.Settings.FindAsync([Key], ct);
        var json = JsonSerializer.Serialize(updated, _json);

        if (row is null)
            db.Settings.Add(new AuthManagerSettingRecord { Key = Key, ValueJson = json });
        else
        {
            row.ValueJson = json;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    public Task<SmsOptions> GetRawSettingsAsync(CancellationToken ct = default) => GetOptionsAsync(ct);

    private async Task<SmsOptions> GetOptionsAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;

        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Settings.FindAsync([Key], ct);
        _cache = row is null
            ? _monitor.CurrentValue.Sms
            : JsonSerializer.Deserialize<SmsOptions>(row.ValueJson, _json) ?? _monitor.CurrentValue.Sms;
        return _cache;
    }

    /// <summary>Shows only the last 4 characters — enough to recognize which key is saved without exposing it.</summary>
    private static string Mask(string secret) =>
        string.IsNullOrEmpty(secret) ? string.Empty : $"••••••••{secret[^Math.Min(4, secret.Length)..]}";
}
