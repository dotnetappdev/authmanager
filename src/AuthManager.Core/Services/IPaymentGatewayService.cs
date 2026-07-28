namespace AuthManager.Core.Services;

/// <summary>
/// Starts and reconciles payments through whichever provider(s) are enabled in
/// <see cref="Options.PaymentOptions"/> — Stripe Checkout Sessions (subscription mode) and
/// PayPal orders/subscriptions (Orders v2 for one-time plans, Billing Subscriptions v1 for
/// recurring plans). This is the "give me a URL to redirect the customer to" +
/// "a webhook/return told us the payment went through, go update our records" surface;
/// provider credentials themselves are managed through <see cref="IPaymentSettingsService"/>.
/// </summary>
public interface IPaymentGatewayService
{
    /// <summary>
    /// Creates a Stripe Checkout Session (subscription mode, or payment mode for a
    /// <c>OneTime</c>-interval plan) for the given customer + plan and returns the hosted
    /// URL to redirect the browser to. Requires Stripe to be enabled and configured with a secret key.
    /// </summary>
    Task<(bool Success, string[] Errors, string? CheckoutUrl)> CreateStripeCheckoutSessionAsync(
        string customerId, string planId, CancellationToken ct = default);

    /// <summary>
    /// Verifies the <c>Stripe-Signature</c> header against the configured webhook signing
    /// secret and, on <c>checkout.session.completed</c> / <c>customer.subscription.updated</c> /
    /// <c>customer.subscription.deleted</c>, creates or updates the matching subscription record.
    /// </summary>
    Task<(bool Success, string[] Errors)> HandleStripeWebhookAsync(
        string payload, string? stripeSignatureHeader, CancellationToken ct = default);

    /// <summary>
    /// Starts a PayPal checkout for the given customer + plan. <c>OneTime</c>-interval plans use
    /// the Orders v2 API (single capture); all other intervals use the Billing Subscriptions v1
    /// API (recurring, auto-creating a Product + Plan for the internal plan on first use).
    /// Returns the "approve" URL to redirect the browser to.
    /// </summary>
    Task<(bool Success, string[] Errors, string? ApprovalUrl)> CreatePayPalCheckoutAsync(
        string customerId, string planId, CancellationToken ct = default);

    /// <summary>
    /// Captures a buyer-approved PayPal order (the <c>token</c> query param on the return URL
    /// for a one-time-plan checkout) and activates the matching subscription record.
    /// </summary>
    Task<(bool Success, string[] Errors)> HandlePayPalOrderApprovedAsync(
        string orderId, CancellationToken ct = default);

    /// <summary>
    /// Confirms a buyer-approved PayPal recurring subscription (the <c>subscription_id</c>
    /// query param on the return URL for a recurring-plan checkout) and activates the
    /// matching subscription record.
    /// </summary>
    Task<(bool Success, string[] Errors)> HandlePayPalSubscriptionApprovedAsync(
        string paypalSubscriptionId, CancellationToken ct = default);

    /// <summary>
    /// Verifies a PayPal webhook event against the configured Webhook ID via PayPal's
    /// verify-webhook-signature API, and on <c>PAYMENT.CAPTURE.COMPLETED</c> /
    /// <c>BILLING.SUBSCRIPTION.ACTIVATED</c> / <c>BILLING.SUBSCRIPTION.CANCELLED</c>,
    /// creates or updates the matching subscription record.
    /// </summary>
    Task<(bool Success, string[] Errors)> HandlePayPalWebhookAsync(
        string payload, IDictionary<string, string> headers, CancellationToken ct = default);
}
