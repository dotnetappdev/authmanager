using AuthManager.Core.Options;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class SmsSettingsServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetSettingsAsync_defaults_to_disabled_with_no_provider_selected()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISmsSettingsService>();

        var settings = await svc.GetSettingsAsync();

        Assert.False(settings.Enabled);
        Assert.Equal(SmsProvider.None, settings.ActiveProvider);
        Assert.False(settings.TwilioAuthTokenSet);
    }

    [Fact]
    public async Task UpdateSettingsAsync_persists_twilio_credentials_and_masks_secret_on_read_back()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISmsSettingsService>();

        await svc.UpdateSettingsAsync(new UpdateSmsSettingsDto
        {
            Enabled = true,
            ActiveProvider = SmsProvider.Twilio,
            TwilioAccountSid = "AC1234567890",
            TwilioAuthToken = "supersecrettoken9999",
            TwilioFromNumber = "+14155552671",
        });

        var settings = await svc.GetSettingsAsync();

        Assert.True(settings.Enabled);
        Assert.Equal(SmsProvider.Twilio, settings.ActiveProvider);
        Assert.Equal("AC1234567890", settings.TwilioAccountSid);
        Assert.True(settings.TwilioAuthTokenSet);
        Assert.EndsWith("9999", settings.TwilioAuthTokenMasked);
        Assert.DoesNotContain("supersecrettoken9999", settings.TwilioAuthTokenMasked);
    }

    [Fact]
    public async Task UpdateSettingsAsync_with_blank_secret_keeps_the_previously_saved_one()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISmsSettingsService>();

        await svc.UpdateSettingsAsync(new UpdateSmsSettingsDto
        {
            ActiveProvider = SmsProvider.Vonage,
            VonageApiKey = "key-1",
            VonageApiSecret = "original-secret",
        });

        await svc.UpdateSettingsAsync(new UpdateSmsSettingsDto
        {
            ActiveProvider = SmsProvider.Vonage,
            VonageApiKey = "key-2",
            VonageApiSecret = null,
        });

        var raw = await svc.GetRawSettingsAsync();
        Assert.Equal("key-2", raw.Vonage.ApiKey);
        Assert.Equal("original-secret", raw.Vonage.ApiSecret);
    }
}
