using AuthManager.Core.Options;

namespace AuthManager.Core.Services;

/// <summary>
/// Reads and updates Stripe / PayPal payment provider settings at runtime —
/// equivalent to the OAuth Provider and SSO configuration surfaces, but for billing.
///
/// The default implementation persists overrides to the AuthManager settings store so
/// they survive application restarts; secret fields already saved are never round-tripped
/// back out (see <see cref="PaymentSettingsInfo"/>) — only newly submitted values overwrite them.
/// </summary>
public interface IPaymentSettingsService
{
    /// <summary>Get the currently effective payment settings, with secrets masked.</summary>
    Task<PaymentSettingsInfo> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>Persist updated payment settings. Blank secret fields leave the previously saved secret untouched.</summary>
    Task UpdateSettingsAsync(UpdatePaymentSettingsDto dto, CancellationToken ct = default);

    /// <summary>
    /// Get the currently effective payment settings with secrets in the clear — for
    /// server-side use only (e.g. <c>IPaymentGatewayService</c> calling out to Stripe/PayPal).
    /// Never expose this over an API surface; use <see cref="GetSettingsAsync"/> for that.
    /// </summary>
    Task<PaymentOptions> GetRawSettingsAsync(CancellationToken ct = default);
}

/// <summary>Payment settings as returned to the UI — secret fields are masked, never echoed in full.</summary>
public sealed class PaymentSettingsInfo
{
    public bool EnablePayments { get; set; }

    public bool StripeEnabled { get; set; }
    public string StripePublishableKey { get; set; } = string.Empty;
    public bool StripeSecretKeySet { get; set; }
    public string StripeSecretKeyMasked { get; set; } = string.Empty;
    public bool StripeWebhookSigningSecretSet { get; set; }
    public string StripeWebhookSigningSecretMasked { get; set; } = string.Empty;
    public string StripeCurrency { get; set; } = "usd";
    public string StripeSuccessUrl { get; set; } = string.Empty;
    public string StripeCancelUrl { get; set; } = string.Empty;

    public bool PayPalEnabled { get; set; }
    public string PayPalClientId { get; set; } = string.Empty;
    public bool PayPalClientSecretSet { get; set; }
    public string PayPalClientSecretMasked { get; set; } = string.Empty;
    public bool PayPalWebhookIdSet { get; set; }
    public string PayPalWebhookIdMasked { get; set; } = string.Empty;
    public bool PayPalUseSandbox { get; set; } = true;
    public string PayPalReturnUrl { get; set; } = string.Empty;
    public string PayPalCancelUrl { get; set; } = string.Empty;
}

/// <summary>
/// Update payload for payment settings. Secret fields (<see cref="StripeSecretKey"/>,
/// <see cref="StripeWebhookSigningSecret"/>, <see cref="PayPalClientSecret"/>,
/// <see cref="PayPalWebhookId"/>) are only overwritten when non-null/non-empty — leave
/// them blank to keep the previously saved value.
/// </summary>
public sealed class UpdatePaymentSettingsDto
{
    public bool EnablePayments { get; set; }

    public bool StripeEnabled { get; set; }
    public string StripePublishableKey { get; set; } = string.Empty;
    public string? StripeSecretKey { get; set; }
    public string? StripeWebhookSigningSecret { get; set; }
    public string StripeCurrency { get; set; } = "usd";
    public string StripeSuccessUrl { get; set; } = string.Empty;
    public string StripeCancelUrl { get; set; } = string.Empty;

    public bool PayPalEnabled { get; set; }
    public string PayPalClientId { get; set; } = string.Empty;
    public string? PayPalClientSecret { get; set; }
    public string? PayPalWebhookId { get; set; }
    public bool PayPalUseSandbox { get; set; } = true;
    public string PayPalReturnUrl { get; set; } = string.Empty;
    public string PayPalCancelUrl { get; set; } = string.Empty;
}
