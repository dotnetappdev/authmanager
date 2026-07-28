using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class PaymentSettingsServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetSettingsAsync_defaults_to_disabled_with_no_secrets_set()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPaymentSettingsService>();

        var settings = await svc.GetSettingsAsync();

        Assert.False(settings.EnablePayments);
        Assert.False(settings.StripeEnabled);
        Assert.False(settings.StripeSecretKeySet);
        Assert.False(settings.PayPalEnabled);
        Assert.False(settings.PayPalClientSecretSet);
    }

    [Fact]
    public async Task UpdateSettingsAsync_persists_values_and_masks_secrets_on_read_back()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPaymentSettingsService>();

        await svc.UpdateSettingsAsync(new UpdatePaymentSettingsDto
        {
            EnablePayments = true,
            StripeEnabled = true,
            StripePublishableKey = "pk_test_123",
            StripeSecretKey = "sk_test_abcdef1234",
            StripeCurrency = "usd",
            PayPalEnabled = true,
            PayPalClientId = "paypal-client",
            PayPalClientSecret = "paypal-secret-xyz",
            PayPalUseSandbox = true,
        });

        var settings = await svc.GetSettingsAsync();

        Assert.True(settings.EnablePayments);
        Assert.True(settings.StripeEnabled);
        Assert.Equal("pk_test_123", settings.StripePublishableKey);
        Assert.True(settings.StripeSecretKeySet);
        Assert.EndsWith("1234", settings.StripeSecretKeyMasked);
        Assert.DoesNotContain("sk_test_abcdef1234", settings.StripeSecretKeyMasked);
        Assert.True(settings.PayPalEnabled);
        Assert.True(settings.PayPalClientSecretSet);
    }

    [Fact]
    public async Task UpdateSettingsAsync_with_blank_secret_keeps_the_previously_saved_one()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPaymentSettingsService>();

        await svc.UpdateSettingsAsync(new UpdatePaymentSettingsDto
        {
            StripeEnabled = true,
            StripePublishableKey = "pk_test_123",
            StripeSecretKey = "sk_test_original",
        });

        // Second save omits the secret — simulates the UI leaving the field blank.
        await svc.UpdateSettingsAsync(new UpdatePaymentSettingsDto
        {
            StripeEnabled = true,
            StripePublishableKey = "pk_test_456",
            StripeSecretKey = null,
        });

        var raw = await svc.GetRawSettingsAsync();
        Assert.Equal("pk_test_456", raw.Stripe.PublishableKey);
        Assert.Equal("sk_test_original", raw.Stripe.SecretKey);
    }
}
