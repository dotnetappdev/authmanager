namespace AuthManager.Core.Options;

/// <summary>
/// Payment provider integration — lets subscriptions be billed through Stripe and/or
/// PayPal instead of (or alongside) purely internal subscription records.
/// Configurable at runtime via /authmanager/payments; values set here are just the
/// startup defaults, same as <see cref="SecurityPolicyOptions"/>.
/// </summary>
public sealed class PaymentOptions
{
    /// <summary>Master switch — when false, no payment provider UI/endpoints are active
    /// regardless of the individual provider <c>Enabled</c> flags. Default: false.</summary>
    public bool EnablePayments { get; set; } = false;

    /// <summary>Stripe Checkout + Billing integration settings.</summary>
    public StripeOptions Stripe { get; set; } = new();

    /// <summary>PayPal Subscriptions (REST v2) integration settings.</summary>
    public PayPalOptions PayPal { get; set; } = new();
}

/// <summary>
/// Stripe integration settings — the values Stripe's own dashboard hands you for a
/// website integration: a publishable key (safe for the browser), a secret key
/// (server-side only), and a webhook signing secret used to verify inbound
/// <c>checkout.session.completed</c> / <c>customer.subscription.*</c> events.
/// </summary>
public sealed class StripeOptions
{
    /// <summary>Enable Stripe as a payment provider for subscriptions. Default: false.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Publishable key (<c>pk_test_...</c> / <c>pk_live_...</c>) — safe to expose client-side.</summary>
    public string PublishableKey { get; set; } = string.Empty;

    /// <summary>Secret key (<c>sk_test_...</c> / <c>sk_live_...</c>) — server-side only, never rendered back to the UI once saved.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Webhook signing secret (<c>whsec_...</c>) used to verify the <c>Stripe-Signature</c> header on inbound webhook events.</summary>
    public string WebhookSigningSecret { get; set; } = string.Empty;

    /// <summary>Three-letter ISO currency code used for Checkout Sessions. Default: "usd".</summary>
    public string Currency { get; set; } = "usd";

    /// <summary>URL Stripe redirects to after a successful Checkout. Supports the <c>{CHECKOUT_SESSION_ID}</c> template placeholder.</summary>
    public string SuccessUrl { get; set; } = string.Empty;

    /// <summary>URL Stripe redirects to when the customer cancels out of Checkout.</summary>
    public string CancelUrl { get; set; } = string.Empty;
}

/// <summary>
/// PayPal integration settings — the REST app credentials from the PayPal Developer
/// Dashboard, used against the Subscriptions v2 API.
/// </summary>
public sealed class PayPalOptions
{
    /// <summary>Enable PayPal as a payment provider for subscriptions. Default: false.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>REST app Client ID from the PayPal Developer Dashboard.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>REST app Client Secret — server-side only, never rendered back to the UI once saved.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Webhook ID (from the configured webhook in the PayPal Dashboard) used to verify inbound webhook event signatures.</summary>
    public string WebhookId { get; set; } = string.Empty;

    /// <summary>When true, calls go to PayPal's sandbox API (api-m.sandbox.paypal.com) instead of live. Default: true.</summary>
    public bool UseSandbox { get; set; } = true;

    /// <summary>URL PayPal redirects to after the buyer approves the subscription.</summary>
    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>URL PayPal redirects to if the buyer cancels approval.</summary>
    public string CancelUrl { get; set; } = string.Empty;
}
