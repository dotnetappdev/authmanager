using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Resolves whether a <see cref="TenantFeature"/> is enabled for a given tenant — the runtime
/// check a host app calls to gate its own pages/endpoints per tenant (same "AuthManager owns
/// the primitive, the host wires it in" shape as <c>IOtpService</c>/<c>ISmsSenderService</c>).
///
/// Resolution order: the tenant's own override (set via the Tenants dashboard) wins; if unset,
/// falls back to the feature's global default (<c>PaymentOptions.EnablePayments</c> for
/// <see cref="TenantFeature.Payments"/>, <c>SmsOptions.Enabled</c> for
/// <see cref="TenantFeature.SmsOtp"/>, <c>WebhookOptions.Enabled</c> for
/// <see cref="TenantFeature.Webhooks"/>, and <c>true</c> for every other feature, since they
/// have no existing global on/off switch of their own).
/// </summary>
public interface ITenantFeatureService
{
    /// <summary>
    /// Is <paramref name="feature"/> enabled for <paramref name="tenantId"/>? Pass null or the
    /// empty string for the root/unassigned tenant, or when multi-tenancy is disabled entirely
    /// (falls straight through to the global default).
    /// </summary>
    Task<bool> IsEnabledAsync(string? tenantId, TenantFeature feature, CancellationToken ct = default);

    /// <summary>Every feature's effective (resolved) state for a tenant — powers the Feature Flags panel on the Tenants dashboard.</summary>
    Task<Dictionary<TenantFeature, bool>> GetEffectiveFlagsAsync(string? tenantId, CancellationToken ct = default);
}
