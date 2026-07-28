using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

internal sealed class TenantFeatureService : ITenantFeatureService
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly IPaymentSettingsService _payments;
    private readonly ISmsSettingsService _sms;
    private readonly IOptionsMonitor<AuthManagerOptions> _options;

    public TenantFeatureService(
        IDbContextFactory<AuthManagerDbContext> factory,
        IPaymentSettingsService payments,
        ISmsSettingsService sms,
        IOptionsMonitor<AuthManagerOptions> options)
    {
        _factory  = factory;
        _payments = payments;
        _sms      = sms;
        _options  = options;
    }

    public async Task<bool> IsEnabledAsync(string? tenantId, TenantFeature feature, CancellationToken ct = default)
    {
        var overrides = await GetOverridesAsync(tenantId, ct);
        return overrides.TryGetValue(feature, out var overridden) ? overridden : await GetGlobalDefaultAsync(feature, ct);
    }

    public async Task<Dictionary<TenantFeature, bool>> GetEffectiveFlagsAsync(string? tenantId, CancellationToken ct = default)
    {
        var overrides = await GetOverridesAsync(tenantId, ct);
        var result = new Dictionary<TenantFeature, bool>();
        foreach (var feature in Enum.GetValues<TenantFeature>())
            result[feature] = overrides.TryGetValue(feature, out var overridden) ? overridden : await GetGlobalDefaultAsync(feature, ct);
        return result;
    }

    private async Task<Dictionary<TenantFeature, bool>> GetOverridesAsync(string? tenantId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tenantId)) return [];

        await using var db = await _factory.CreateDbContextAsync(ct);
        var tenant = await db.Tenants.FindAsync([tenantId], ct);
        if (tenant is null) return [];

        try
        {
            return JsonSerializer.Deserialize<Dictionary<TenantFeature, bool>>(tenant.FeatureFlagsJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // Payments/SMS have their own persisted "enabled" settings service (the runtime-effective
    // value can differ from the AuthManagerOptions startup default), so those two go through
    // their settings service rather than IOptionsMonitor directly. Webhooks has no such
    // runtime-editable store yet, so it reads straight from options. Every other feature has no
    // existing global on/off switch at all, so it's globally on unless a tenant opts out.
    private async Task<bool> GetGlobalDefaultAsync(TenantFeature feature, CancellationToken ct) => feature switch
    {
        TenantFeature.Payments => (await _payments.GetRawSettingsAsync(ct)).EnablePayments,
        TenantFeature.SmsOtp   => (await _sms.GetRawSettingsAsync(ct)).Enabled,
        TenantFeature.Webhooks => _options.CurrentValue.Webhooks.Enabled,
        _ => true,
    };
}
