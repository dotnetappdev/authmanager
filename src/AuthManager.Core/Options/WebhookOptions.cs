namespace AuthManager.Core.Options;

/// <summary>
/// HTTP webhook configuration — fire-and-forget HTTP POSTs when auth events occur.
/// Inspired by Firebase Auth blocking functions and Keycloak event listeners.
///
/// Each registered endpoint receives a signed JSON payload:
/// <code>
/// {
///   "event":     "user.created",
///   "timestamp": "2026-01-01T00:00:00Z",
///   "userId":    "...",
///   "email":     "...",
///   "data":      { ... event-specific fields ... }
/// }
/// </code>
///
/// The request includes an <c>X-AuthManager-Signature</c> header — an HMAC-SHA256
/// of the raw body using the endpoint's <see cref="WebhookEndpoint.Secret"/>.
/// </summary>
public sealed class WebhookOptions
{
    /// <summary>Enable the webhook dispatcher. Default: false.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Endpoints to call. Each can subscribe to a different set of events.
    /// </summary>
    public List<WebhookEndpoint> Endpoints { get; set; } = [];

    /// <summary>Per-request timeout for webhook deliveries. Default: 10 s.</summary>
    public TimeSpan DeliveryTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Retry failed deliveries up to this many times. Default: 2.</summary>
    public int MaxRetries { get; set; } = 2;
}

/// <summary>
/// A single webhook endpoint subscription.
/// </summary>
public sealed class WebhookEndpoint
{
    /// <summary>Friendly name for display in the UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Full URL to POST events to.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Shared secret used to sign payloads with HMAC-SHA256.
    /// The receiving server should verify <c>X-AuthManager-Signature</c>.
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Wildcard <c>"*"</c> subscribes to all events.
    /// Otherwise list specific events from <see cref="WebhookEventNames"/>.
    /// </summary>
    public List<string> Events { get; set; } = ["*"];

    /// <summary>Toggle without removing the endpoint. Default: true.</summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>Well-known event name constants for <see cref="WebhookEndpoint.Events"/>.</summary>
public static class WebhookEventNames
{
    public const string UserCreated         = "user.created";
    public const string UserUpdated         = "user.updated";
    public const string UserDeleted         = "user.deleted";
    public const string UserLogin           = "user.login";
    public const string UserLoginFailed     = "user.login_failed";
    public const string UserLockout         = "user.lockout";
    public const string UserUnlocked        = "user.unlocked";
    public const string PasswordChanged     = "user.password_changed";
    public const string PasswordReset       = "user.password_reset";
    public const string EmailVerified       = "user.email_verified";
    public const string TwoFactorEnabled    = "user.2fa_enabled";
    public const string TwoFactorDisabled   = "user.2fa_disabled";
    public const string RoleAssigned        = "user.role_assigned";
    public const string RoleRemoved         = "user.role_removed";
    public const string ClaimAdded          = "user.claim_added";
    public const string ClaimRemoved        = "user.claim_removed";
    public const string TokenRevoked        = "token.revoked";
    public const string All                 = "*";
}
