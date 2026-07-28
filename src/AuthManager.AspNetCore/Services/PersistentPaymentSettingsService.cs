using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// SQLite-backed payment provider settings service. Mirrors
/// <see cref="PersistentSecurityPolicyService"/>: reads/writes a JSON-serialized
/// <see cref="PaymentOptions"/> under a single settings key, falling back to
/// <see cref="AuthManagerOptions"/> defaults when nothing has been persisted yet.
/// </summary>
internal sealed class PersistentPaymentSettingsService : IPaymentSettingsService
{
    private const string Key = "PaymentOptions";

    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly IOptionsMonitor<AuthManagerOptions>     _monitor;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private PaymentOptions? _cache;

    public PersistentPaymentSettingsService(
        IDbContextFactory<AuthManagerDbContext> factory,
        IOptionsMonitor<AuthManagerOptions> monitor)
    {
        _factory = factory;
        _monitor = monitor;
    }

    public async Task<PaymentSettingsInfo> GetSettingsAsync(CancellationToken ct = default)
    {
        var options = await GetOptionsAsync(ct);
        return new PaymentSettingsInfo
        {
            EnablePayments = options.EnablePayments,

            StripeEnabled = options.Stripe.Enabled,
            StripePublishableKey = options.Stripe.PublishableKey,
            StripeSecretKeySet = !string.IsNullOrEmpty(options.Stripe.SecretKey),
            StripeSecretKeyMasked = Mask(options.Stripe.SecretKey),
            StripeWebhookSigningSecretSet = !string.IsNullOrEmpty(options.Stripe.WebhookSigningSecret),
            StripeWebhookSigningSecretMasked = Mask(options.Stripe.WebhookSigningSecret),
            StripeCurrency = options.Stripe.Currency,
            StripeSuccessUrl = options.Stripe.SuccessUrl,
            StripeCancelUrl = options.Stripe.CancelUrl,

            PayPalEnabled = options.PayPal.Enabled,
            PayPalClientId = options.PayPal.ClientId,
            PayPalClientSecretSet = !string.IsNullOrEmpty(options.PayPal.ClientSecret),
            PayPalClientSecretMasked = Mask(options.PayPal.ClientSecret),
            PayPalWebhookIdSet = !string.IsNullOrEmpty(options.PayPal.WebhookId),
            PayPalWebhookIdMasked = Mask(options.PayPal.WebhookId),
            PayPalUseSandbox = options.PayPal.UseSandbox,
            PayPalReturnUrl = options.PayPal.ReturnUrl,
            PayPalCancelUrl = options.PayPal.CancelUrl,
        };
    }

    public async Task UpdateSettingsAsync(UpdatePaymentSettingsDto dto, CancellationToken ct = default)
    {
        var current = await GetOptionsAsync(ct);

        var updated = new PaymentOptions
        {
            EnablePayments = dto.EnablePayments,
            Stripe = new StripeOptions
            {
                Enabled = dto.StripeEnabled,
                PublishableKey = dto.StripePublishableKey,
                SecretKey = string.IsNullOrEmpty(dto.StripeSecretKey) ? current.Stripe.SecretKey : dto.StripeSecretKey,
                WebhookSigningSecret = string.IsNullOrEmpty(dto.StripeWebhookSigningSecret)
                    ? current.Stripe.WebhookSigningSecret : dto.StripeWebhookSigningSecret,
                Currency = string.IsNullOrWhiteSpace(dto.StripeCurrency) ? "usd" : dto.StripeCurrency,
                SuccessUrl = dto.StripeSuccessUrl,
                CancelUrl = dto.StripeCancelUrl,
            },
            PayPal = new PayPalOptions
            {
                Enabled = dto.PayPalEnabled,
                ClientId = dto.PayPalClientId,
                ClientSecret = string.IsNullOrEmpty(dto.PayPalClientSecret) ? current.PayPal.ClientSecret : dto.PayPalClientSecret,
                WebhookId = string.IsNullOrEmpty(dto.PayPalWebhookId) ? current.PayPal.WebhookId : dto.PayPalWebhookId,
                UseSandbox = dto.PayPalUseSandbox,
                ReturnUrl = dto.PayPalReturnUrl,
                CancelUrl = dto.PayPalCancelUrl,
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

    public Task<PaymentOptions> GetRawSettingsAsync(CancellationToken ct = default) => GetOptionsAsync(ct);

    private async Task<PaymentOptions> GetOptionsAsync(CancellationToken ct)
    {
        if (_cache is not null) return _cache;

        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Settings.FindAsync([Key], ct);
        _cache = row is null
            ? _monitor.CurrentValue.Payments
            : JsonSerializer.Deserialize<PaymentOptions>(row.ValueJson, _json) ?? _monitor.CurrentValue.Payments;
        return _cache;
    }

    /// <summary>Shows only the last 4 characters — enough to recognize which key is saved without exposing it.</summary>
    private static string Mask(string secret) =>
        string.IsNullOrEmpty(secret) ? string.Empty : $"••••••••{secret[^Math.Min(4, secret.Length)..]}";
}
