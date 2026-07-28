using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class TenantFeatureServiceTests : ServiceTestBase
{
    protected override void ConfigureOptions(AuthManagerOptions options)
    {
        options.MultiTenancy.Enabled = true;
    }

    [Fact]
    public async Task IsEnabledAsync_with_no_tenant_falls_through_to_global_default()
    {
        using var scope = CreateScope();
        var features = scope.ServiceProvider.GetRequiredService<ITenantFeatureService>();

        // No global on/off switch exists for Sso/Passkeys/etc — they default to enabled.
        Assert.True(await features.IsEnabledAsync(null, TenantFeature.Sso));
        Assert.True(await features.IsEnabledAsync(null, TenantFeature.Passkeys));

        // Payments/SmsOtp follow their own settings service default (both start disabled).
        Assert.False(await features.IsEnabledAsync(null, TenantFeature.Payments));
        Assert.False(await features.IsEnabledAsync(null, TenantFeature.SmsOtp));
    }

    [Fact]
    public async Task IsEnabledAsync_honors_a_tenant_override_over_the_global_default()
    {
        using var scope = CreateScope();
        var tenants = scope.ServiceProvider.GetRequiredService<ITenantService>();
        var features = scope.ServiceProvider.GetRequiredService<ITenantFeatureService>();

        await tenants.CreateTenantAsync(new CreateTenantDto { Id = "acme", DisplayName = "Acme" });
        await tenants.UpdateTenantAsync("acme", new UpdateTenantDto
        {
            DisplayName = "Acme",
            FeatureOverrides = new Dictionary<TenantFeature, bool> { [TenantFeature.Sso] = false },
        });

        // Overridden feature reflects the tenant's own setting...
        Assert.False(await features.IsEnabledAsync("acme", TenantFeature.Sso));
        // ...while an untouched feature still falls back to the global default.
        Assert.True(await features.IsEnabledAsync("acme", TenantFeature.Passkeys));
    }

    [Fact]
    public async Task GetEffectiveFlagsAsync_returns_every_feature_resolved()
    {
        using var scope = CreateScope();
        var tenants = scope.ServiceProvider.GetRequiredService<ITenantService>();
        var features = scope.ServiceProvider.GetRequiredService<ITenantFeatureService>();

        await tenants.CreateTenantAsync(new CreateTenantDto { Id = "globex", DisplayName = "Globex" });
        await tenants.UpdateTenantAsync("globex", new UpdateTenantDto
        {
            DisplayName = "Globex",
            FeatureOverrides = new Dictionary<TenantFeature, bool> { [TenantFeature.Webhooks] = true },
        });

        var flags = await features.GetEffectiveFlagsAsync("globex");

        Assert.Equal(Enum.GetValues<TenantFeature>().Length, flags.Count);
        Assert.True(flags[TenantFeature.Webhooks]);
        Assert.True(flags[TenantFeature.Licensing]); // no override, no global switch — defaults true
    }
}
