namespace AuthManager.Core.Models;

/// <summary>
/// Toggleable feature areas that can be enabled/disabled per tenant from the Tenants
/// dashboard. A tenant's flag is an override — when unset, the feature falls back to its
/// global default (see <see cref="Services.ITenantFeatureService"/>).
/// </summary>
public enum TenantFeature
{
    /// <summary>Single Sign-On (Entra ID / OIDC / SAML).</summary>
    Sso,

    /// <summary>Passkeys (WebAuthn) registration and sign-in.</summary>
    Passkeys,

    /// <summary>Stripe/PayPal payment checkout. Global default follows <c>PaymentOptions.EnablePayments</c>.</summary>
    Payments,

    /// <summary>SMS OTP delivery. Global default follows <c>SmsOptions.Enabled</c>.</summary>
    SmsOtp,

    /// <summary>Outbound auth-event webhooks. Global default follows <c>WebhookOptions.Enabled</c>.</summary>
    Webhooks,

    /// <summary>License key generation/activation.</summary>
    Licensing,

    /// <summary>Personal access tokens (API Tokens page).</summary>
    ApiTokens,

    /// <summary>OAuth2 service-to-service client-credentials clients.</summary>
    OAuthClients,
}
