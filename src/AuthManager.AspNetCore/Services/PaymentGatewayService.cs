using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Calls out to the Stripe and PayPal REST APIs directly (no SDK dependency) using named
/// <see cref="IHttpClientFactory"/> clients ("AuthManager.Stripe" / "AuthManager.PayPal")
/// registered in <c>ServiceCollectionExtensions.AddAuthManager</c>.
/// </summary>
internal sealed class PaymentGatewayService : IPaymentGatewayService
{
    private readonly IDbContextFactory<AuthManagerDbContext> _dbFactory;
    private readonly IPaymentSettingsService _settings;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PaymentGatewayService> _logger;

    public PaymentGatewayService(
        IDbContextFactory<AuthManagerDbContext> dbFactory,
        IPaymentSettingsService settings,
        IHttpClientFactory httpFactory,
        ILogger<PaymentGatewayService> logger)
    {
        _dbFactory = dbFactory;
        _settings = settings;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    // ───────────────────────── Stripe ─────────────────────────

    public async Task<(bool Success, string[] Errors, string? CheckoutUrl)> CreateStripeCheckoutSessionAsync(
        string customerId, string planId, CancellationToken ct = default)
    {
        var options = await _settings.GetRawSettingsAsync(ct);
        if (!options.EnablePayments || !options.Stripe.Enabled)
            return (false, ["Stripe is not enabled. Configure it under Payment Settings first."], null);
        if (string.IsNullOrEmpty(options.Stripe.SecretKey))
            return (false, ["Stripe secret key is not configured."], null);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var customer = await db.Customers.FindAsync([customerId], ct);
        if (customer is null) return (false, ["Customer not found."], null);
        var plan = await db.SubscriptionPlans.FindAsync([planId], ct);
        if (plan is null) return (false, ["Plan not found."], null);

        var isOneTime = plan.Interval == "OneTime";
        var form = new Dictionary<string, string>
        {
            ["mode"] = isOneTime ? "payment" : "subscription",
            ["customer_email"] = customer.Email,
            ["success_url"] = string.IsNullOrWhiteSpace(options.Stripe.SuccessUrl) ? "https://example.com/success" : options.Stripe.SuccessUrl,
            ["cancel_url"] = string.IsNullOrWhiteSpace(options.Stripe.CancelUrl) ? "https://example.com/cancel" : options.Stripe.CancelUrl,
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = plan.Currency.ToLowerInvariant(),
            ["line_items[0][price_data][product_data][name]"] = plan.Name,
            ["line_items[0][price_data][unit_amount]"] = plan.PriceCents.ToString(),
            ["metadata[customerId]"] = customerId,
            ["metadata[planId]"] = planId,
        };
        if (!isOneTime)
            form["line_items[0][price_data][recurring][interval]"] = plan.Interval switch
            {
                "Yearly" => "year",
                "Weekly" => "week",
                _ => "month",
            };

        var client = _httpFactory.CreateClient("AuthManager.Stripe");
        client.DefaultRequestHeaders.Authorization = new("Bearer", options.Stripe.SecretKey);

        try
        {
            using var resp = await client.PostAsync("v1/checkout/sessions", new FormUrlEncodedContent(form), ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Stripe Checkout Session creation failed: {Status} {Body}", resp.StatusCode, body);
                return (false, [ExtractApiError(body, "Stripe rejected the checkout request.")], null);
            }

            using var json = JsonDocument.Parse(body);
            var url = json.RootElement.TryGetProperty("url", out var u) ? u.GetString() : null;
            return string.IsNullOrEmpty(url)
                ? (false, ["Stripe did not return a checkout URL."], null)
                : (true, [], url);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Stripe Checkout Session request failed");
            return (false, ["Could not reach Stripe. Check network connectivity and try again."], null);
        }
    }

    public async Task<(bool Success, string[] Errors)> HandleStripeWebhookAsync(
        string payload, string? stripeSignatureHeader, CancellationToken ct = default)
    {
        var options = await _settings.GetRawSettingsAsync(ct);
        if (!options.Stripe.Enabled) return (false, ["Stripe is not enabled."]);

        if (!string.IsNullOrEmpty(options.Stripe.WebhookSigningSecret))
        {
            if (!VerifyStripeSignature(payload, stripeSignatureHeader, options.Stripe.WebhookSigningSecret))
                return (false, ["Webhook signature verification failed."]);
        }

        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;
        var eventType = root.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (eventType is null) return (false, ["Missing event type."]);

        var data = root.GetProperty("data").GetProperty("object");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        switch (eventType)
        {
            case "checkout.session.completed":
            {
                var metadata = data.TryGetProperty("metadata", out var m) ? m : default;
                var customerId = metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty("customerId", out var cid) ? cid.GetString() : null;
                var planId = metadata.ValueKind == JsonValueKind.Object && metadata.TryGetProperty("planId", out var pid) ? pid.GetString() : null;
                if (customerId is null || planId is null) return (false, ["Checkout session is missing customerId/planId metadata."]);

                var plan = await db.SubscriptionPlans.FindAsync([planId], ct);
                if (plan is null) return (false, ["Plan referenced by checkout session no longer exists."]);

                var stripeCustomerId = data.TryGetProperty("customer", out var sc) ? sc.GetString() : null;
                var stripeSubscriptionId = data.TryGetProperty("subscription", out var ss) ? ss.GetString() : null;
                var sessionId = data.TryGetProperty("id", out var sid) ? sid.GetString() : null;

                await UpsertSubscriptionAsync(db, customerId, planId, plan, "Stripe", stripeCustomerId, stripeSubscriptionId, sessionId, ct);
                return (true, []);
            }
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
            {
                var stripeSubscriptionId = data.TryGetProperty("id", out var sid) ? sid.GetString() : null;
                if (stripeSubscriptionId is null) return (true, []);

                var sub = await db.CustomerSubscriptions.FirstOrDefaultAsync(s => s.ExternalSubscriptionId == stripeSubscriptionId, ct);
                if (sub is null) return (true, []); // Not something we're tracking — ignore.

                if (eventType == "customer.subscription.deleted")
                {
                    sub.Status = "Canceled";
                    sub.CanceledAt = DateTimeOffset.UtcNow;
                }
                else if (data.TryGetProperty("current_period_end", out var cpe) && cpe.TryGetInt64(out var unixSeconds))
                {
                    sub.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                }
                await db.SaveChangesAsync(ct);
                return (true, []);
            }
            default:
                return (true, []); // Unhandled event types are not errors.
        }
    }

    private static bool VerifyStripeSignature(string payload, string? header, string secret)
    {
        if (string.IsNullOrEmpty(header)) return false;

        string? timestamp = null, signature = null;
        foreach (var part in header.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "t") timestamp = kv[1];
            else if (kv[0] == "v1") signature = kv[1];
        }
        if (timestamp is null || signature is null) return false;

        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload)));
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }

    // ───────────────────────── PayPal ─────────────────────────

    public async Task<(bool Success, string[] Errors, string? ApprovalUrl)> CreatePayPalCheckoutAsync(
        string customerId, string planId, CancellationToken ct = default)
    {
        var options = await _settings.GetRawSettingsAsync(ct);
        if (!options.EnablePayments || !options.PayPal.Enabled)
            return (false, ["PayPal is not enabled. Configure it under Payment Settings first."], null);
        if (string.IsNullOrEmpty(options.PayPal.ClientId) || string.IsNullOrEmpty(options.PayPal.ClientSecret))
            return (false, ["PayPal client ID/secret is not configured."], null);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var customer = await db.Customers.FindAsync([customerId], ct);
        if (customer is null) return (false, ["Customer not found."], null);
        var plan = await db.SubscriptionPlans.FindAsync([planId], ct);
        if (plan is null) return (false, ["Plan not found."], null);

        var client = _httpFactory.CreateClient("AuthManager.PayPal");
        client.BaseAddress = new Uri(options.PayPal.UseSandbox
            ? "https://api-m.sandbox.paypal.com/" : "https://api-m.paypal.com/");

        var (tokenOk, tokenErrors, accessToken) = await GetPayPalAccessTokenAsync(client, options.PayPal, ct);
        if (!tokenOk) return (false, tokenErrors, null);

        var priceValue = (plan.PriceCents / 100m).ToString("F2");
        var returnUrl = string.IsNullOrWhiteSpace(options.PayPal.ReturnUrl) ? "https://example.com/paypal/return" : options.PayPal.ReturnUrl;
        var cancelUrl = string.IsNullOrWhiteSpace(options.PayPal.CancelUrl) ? "https://example.com/paypal/cancel" : options.PayPal.CancelUrl;

        return plan.Interval == "OneTime"
            ? await CreatePayPalOrderAsync(client, accessToken!, customerId, planId, plan.Name, plan.Currency, priceValue, returnUrl, cancelUrl, ct)
            : await CreatePayPalSubscriptionAsync(client, accessToken!, customerId, planId, plan, priceValue, returnUrl, cancelUrl, ct);
    }

    private async Task<(bool, string[], string?)> CreatePayPalOrderAsync(
        HttpClient client, string accessToken, string customerId, string planId,
        string planName, string currency, string priceValue, string returnUrl, string cancelUrl, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    custom_id = $"{customerId}|{planId}",
                    description = planName,
                    amount = new { currency_code = currency.ToUpperInvariant(), value = priceValue }
                }
            },
            application_context = new { return_url = returnUrl, cancel_url = cancelUrl }
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, "v2/checkout/orders");
        req.Headers.Authorization = new("Bearer", accessToken);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var resp = await client.SendAsync(req, ct);
        var respBody = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayPal order creation failed: {Status} {Body}", resp.StatusCode, respBody);
            return (false, [ExtractApiError(respBody, "PayPal rejected the order request.")], null);
        }

        using var json = JsonDocument.Parse(respBody);
        var approveUrl = json.RootElement.GetProperty("links").EnumerateArray()
            .FirstOrDefault(l => l.GetProperty("rel").GetString() == "approve")
            .TryGetPropertyOrNull("href");
        return approveUrl is null ? (false, ["PayPal did not return an approval link."], null) : (true, [], approveUrl);
    }

    private async Task<(bool, string[], string?)> CreatePayPalSubscriptionAsync(
        HttpClient client, string accessToken, string customerId, string planId,
        SubscriptionPlanRecord plan, string priceValue, string returnUrl, string cancelUrl, CancellationToken ct)
    {
        // Auto-creates a fresh PayPal Product + Billing Plan on every checkout. A production
        // deployment would cache the resulting PayPal product/plan IDs against SubscriptionPlanRecord
        // instead of re-creating them each time — left out here to keep the DB schema change minimal.
        var (productOk, productErrors, productId) = await PostPayPalJsonAsync(client, accessToken, "v1/catalogs/products",
            new { name = plan.Name, description = plan.Description ?? plan.Name, type = "SERVICE" }, "id", ct);
        if (!productOk) return (false, productErrors, null);

        var intervalUnit = plan.Interval switch { "Yearly" => "YEAR", "Weekly" => "WEEK", _ => "MONTH" };
        var (planOk, planErrors, paypalPlanId) = await PostPayPalJsonAsync(client, accessToken, "v1/billing/plans",
            new
            {
                product_id = productId,
                name = plan.Name,
                billing_cycles = new[]
                {
                    new
                    {
                        frequency = new { interval_unit = intervalUnit, interval_count = 1 },
                        tenure_type = "REGULAR",
                        sequence = 1,
                        total_cycles = 0,
                        pricing_scheme = new { fixed_price = new { value = priceValue, currency_code = plan.Currency.ToUpperInvariant() } }
                    }
                },
                payment_preferences = new { auto_bill_outstanding = true, payment_failure_threshold = 3 }
            }, "id", ct);
        if (!planOk) return (false, planErrors, null);

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/billing/subscriptions");
        req.Headers.Authorization = new("Bearer", accessToken);
        req.Content = new StringContent(JsonSerializer.Serialize(new
        {
            plan_id = paypalPlanId,
            custom_id = $"{customerId}|{planId}",
            application_context = new { return_url = returnUrl, cancel_url = cancelUrl }
        }), Encoding.UTF8, "application/json");

        using var resp = await client.SendAsync(req, ct);
        var respBody = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayPal subscription creation failed: {Status} {Body}", resp.StatusCode, respBody);
            return (false, [ExtractApiError(respBody, "PayPal rejected the subscription request.")], null);
        }

        using var json = JsonDocument.Parse(respBody);
        var approveUrl = json.RootElement.GetProperty("links").EnumerateArray()
            .FirstOrDefault(l => l.GetProperty("rel").GetString() == "approve")
            .TryGetPropertyOrNull("href");
        return approveUrl is null ? (false, ["PayPal did not return an approval link."], null) : (true, [], approveUrl);
    }

    public async Task<(bool Success, string[] Errors)> HandlePayPalOrderApprovedAsync(string orderId, CancellationToken ct = default)
    {
        var options = await _settings.GetRawSettingsAsync(ct);
        if (!options.PayPal.Enabled) return (false, ["PayPal is not enabled."]);

        var client = _httpFactory.CreateClient("AuthManager.PayPal");
        client.BaseAddress = new Uri(options.PayPal.UseSandbox
            ? "https://api-m.sandbox.paypal.com/" : "https://api-m.paypal.com/");
        var (tokenOk, tokenErrors, accessToken) = await GetPayPalAccessTokenAsync(client, options.PayPal, ct);
        if (!tokenOk) return (false, tokenErrors);

        using var captureReq = new HttpRequestMessage(HttpMethod.Post, $"v2/checkout/orders/{orderId}/capture");
        captureReq.Headers.Authorization = new("Bearer", accessToken);
        using var resp = await client.SendAsync(captureReq, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayPal order capture failed: {Status} {Body}", resp.StatusCode, body);
            return (false, [ExtractApiError(body, "PayPal order capture failed.")]);
        }

        using var json = JsonDocument.Parse(body);
        var customId = json.RootElement.GetProperty("purchase_units")[0].TryGetPropertyOrNull("custom_id");
        if (customId is null || !TrySplitCustomId(customId, out var customerId, out var planId))
            return (false, ["Captured order is missing the customer/plan reference."]);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var plan = await db.SubscriptionPlans.FindAsync([planId], ct);
        if (plan is null) return (false, ["Plan referenced by the order no longer exists."]);

        await UpsertSubscriptionAsync(db, customerId, planId, plan, "PayPal", customerId, orderId, orderId, ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> HandlePayPalSubscriptionApprovedAsync(string paypalSubscriptionId, CancellationToken ct = default)
    {
        var options = await _settings.GetRawSettingsAsync(ct);
        if (!options.PayPal.Enabled) return (false, ["PayPal is not enabled."]);

        var client = _httpFactory.CreateClient("AuthManager.PayPal");
        client.BaseAddress = new Uri(options.PayPal.UseSandbox
            ? "https://api-m.sandbox.paypal.com/" : "https://api-m.paypal.com/");
        var (tokenOk, tokenErrors, accessToken) = await GetPayPalAccessTokenAsync(client, options.PayPal, ct);
        if (!tokenOk) return (false, tokenErrors);

        using var req = new HttpRequestMessage(HttpMethod.Get, $"v1/billing/subscriptions/{paypalSubscriptionId}");
        req.Headers.Authorization = new("Bearer", accessToken);
        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("PayPal subscription lookup failed: {Status} {Body}", resp.StatusCode, body);
            return (false, [ExtractApiError(body, "Could not confirm the PayPal subscription.")]);
        }

        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var status = root.TryGetPropertyOrNull("status");
        var customId = root.TryGetPropertyOrNull("custom_id");
        if (customId is null || !TrySplitCustomId(customId, out var customerId, out var planId))
            return (false, ["Subscription is missing the customer/plan reference."]);
        if (!string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            return (false, [$"PayPal subscription is not active yet (status: {status})."]);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var plan = await db.SubscriptionPlans.FindAsync([planId], ct);
        if (plan is null) return (false, ["Plan referenced by the subscription no longer exists."]);

        await UpsertSubscriptionAsync(db, customerId, planId, plan, "PayPal", customerId, paypalSubscriptionId, paypalSubscriptionId, ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> HandlePayPalWebhookAsync(
        string payload, IDictionary<string, string> headers, CancellationToken ct = default)
    {
        var options = await _settings.GetRawSettingsAsync(ct);
        if (!options.PayPal.Enabled) return (false, ["PayPal is not enabled."]);

        if (!string.IsNullOrEmpty(options.PayPal.WebhookId))
        {
            var verified = await VerifyPayPalWebhookAsync(payload, headers, options.PayPal, ct);
            if (!verified) return (false, ["Webhook signature verification failed."]);
        }

        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;
        var eventType = root.TryGetPropertyOrNull("event_type");
        if (eventType is null) return (false, ["Missing event_type."]);

        return eventType switch
        {
            "BILLING.SUBSCRIPTION.ACTIVATED" =>
                await HandlePayPalSubscriptionApprovedAsync(
                    root.GetProperty("resource").GetProperty("id").GetString()!, ct),
            "BILLING.SUBSCRIPTION.CANCELLED" => await CancelPayPalSubscriptionAsync(
                root.GetProperty("resource").GetProperty("id").GetString()!, ct),
            "PAYMENT.CAPTURE.COMPLETED" => (true, []), // Order already reconciled via HandlePayPalOrderApprovedAsync on return.
            _ => (true, []), // Unhandled event types are not errors.
        };
    }

    private async Task<(bool, string[])> CancelPayPalSubscriptionAsync(string paypalSubscriptionId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var sub = await db.CustomerSubscriptions.FirstOrDefaultAsync(s => s.ExternalSubscriptionId == paypalSubscriptionId, ct);
        if (sub is null) return (true, []);
        sub.Status = "Canceled";
        sub.CanceledAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    private static async Task<(bool, string[], string?)> GetPayPalAccessTokenAsync(HttpClient client, PayPalOptions options, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token");
        req.Headers.Authorization = new("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}")));
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            return (false, [ExtractApiError(body, "Could not authenticate with PayPal.")], null);

        using var json = JsonDocument.Parse(body);
        var token = json.RootElement.TryGetPropertyOrNull("access_token");
        return token is null ? (false, ["PayPal did not return an access token."], null) : (true, [], token);
    }

    private static async Task<(bool, string[], string?)> PostPayPalJsonAsync(
        HttpClient client, string accessToken, string path, object body, string resultProperty, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Authorization = new("Bearer", accessToken);
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var resp = await client.SendAsync(req, ct);
        var respBody = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            return (false, [ExtractApiError(respBody, $"PayPal rejected the request to {path}.")], null);

        using var json = JsonDocument.Parse(respBody);
        var value = json.RootElement.TryGetPropertyOrNull(resultProperty);
        return value is null ? (false, [$"PayPal response from {path} is missing '{resultProperty}'."], null) : (true, [], value);
    }

    private async Task<bool> VerifyPayPalWebhookAsync(string payload, IDictionary<string, string> headers, PayPalOptions options, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("AuthManager.PayPal");
        client.BaseAddress = new Uri(options.UseSandbox ? "https://api-m.sandbox.paypal.com/" : "https://api-m.paypal.com/");
        var (tokenOk, _, accessToken) = await GetPayPalAccessTokenAsync(client, options, ct);
        if (!tokenOk) return false;

        headers.TryGetValue("Paypal-Transmission-Id", out var transmissionId);
        headers.TryGetValue("Paypal-Transmission-Time", out var transmissionTime);
        headers.TryGetValue("Paypal-Cert-Url", out var certUrl);
        headers.TryGetValue("Paypal-Auth-Algo", out var authAlgo);
        headers.TryGetValue("Paypal-Transmission-Sig", out var transmissionSig);

        using var req = new HttpRequestMessage(HttpMethod.Post, "v1/notifications/verify-webhook-signature");
        req.Headers.Authorization = new("Bearer", accessToken);
        req.Content = new StringContent(JsonSerializer.Serialize(new
        {
            transmission_id = transmissionId,
            transmission_time = transmissionTime,
            cert_url = certUrl,
            auth_algo = authAlgo,
            transmission_sig = transmissionSig,
            webhook_id = options.WebhookId,
            webhook_event = JsonDocument.Parse(payload).RootElement
        }), Encoding.UTF8, "application/json");

        using var resp = await client.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return false;
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.TryGetPropertyOrNull("verification_status") == "SUCCESS";
    }

    // ───────────────────────── Shared helpers ─────────────────────────

    private static async Task UpsertSubscriptionAsync(
        AuthManagerDbContext db, string customerId, string planId, SubscriptionPlanRecord plan,
        string provider, string? externalCustomerId, string? externalSubscriptionId, string? checkoutSessionId, CancellationToken ct)
    {
        var existing = await db.CustomerSubscriptions.FirstOrDefaultAsync(
            s => s.CustomerId == customerId && s.PlanId == planId && s.Status != "Canceled" && s.Status != "Expired", ct);

        var now = DateTimeOffset.UtcNow;
        var periodEnd = plan.Interval switch
        {
            "Yearly" => now.AddYears(1),
            "Weekly" => now.AddDays(7),
            "OneTime" => now.AddYears(100), // "Never expires" for a one-off purchase.
            _ => now.AddMonths(1),
        };

        if (existing is null)
        {
            db.CustomerSubscriptions.Add(new CustomerSubscriptionRecord
            {
                CustomerId = customerId,
                PlanId = planId,
                Status = "Active",
                StartedAt = now,
                CurrentPeriodEnd = periodEnd,
                PaymentProvider = provider,
                ExternalCustomerId = externalCustomerId,
                ExternalSubscriptionId = externalSubscriptionId,
                ExternalCheckoutSessionId = checkoutSessionId,
            });
        }
        else
        {
            existing.Status = "Active";
            existing.CurrentPeriodEnd = periodEnd;
            existing.PaymentProvider = provider;
            existing.ExternalCustomerId = externalCustomerId;
            existing.ExternalSubscriptionId = externalSubscriptionId;
            existing.ExternalCheckoutSessionId = checkoutSessionId;
        }

        await db.SaveChangesAsync(ct);
    }

    private static bool TrySplitCustomId(string customId, out string customerId, out string planId)
    {
        var parts = customId.Split('|', 2);
        if (parts.Length == 2) { customerId = parts[0]; planId = parts[1]; return true; }
        customerId = planId = string.Empty;
        return false;
    }

    private static string ExtractApiError(string responseBody, string fallback)
    {
        try
        {
            using var json = JsonDocument.Parse(responseBody);
            if (json.RootElement.TryGetProperty("error", out var stripeErr) && stripeErr.TryGetProperty("message", out var msg))
                return msg.GetString() ?? fallback;
            if (json.RootElement.TryGetProperty("message", out var paypalMsg))
                return paypalMsg.GetString() ?? fallback;
        }
        catch (JsonException) { /* not JSON — fall through */ }
        return fallback;
    }
}

file static class JsonElementExtensions
{
    public static string? TryGetPropertyOrNull(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.GetString()
            : null;
}
