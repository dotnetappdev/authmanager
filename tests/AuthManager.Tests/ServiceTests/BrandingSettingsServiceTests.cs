using AuthManager.Core.Options;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class BrandingSettingsServiceTests : ServiceTestBase
{
    [Fact]
    public async Task GetSettingsAsync_defaults_to_empty_branding()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IBrandingSettingsService>();

        var settings = await svc.GetSettingsAsync();

        Assert.Null(settings.CompanyName);
        Assert.Null(settings.LogoUrl);
        Assert.False(settings.HidePoweredByFooter);
    }

    [Fact]
    public async Task UpdateSettingsAsync_persists_company_name_and_logo()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IBrandingSettingsService>();

        await svc.UpdateSettingsAsync(new BrandingOptions
        {
            CompanyName = "Acme Identity",
            LogoUrl = "https://acme.example/logo.png",
            SupportEmail = "support@acme.example",
            HidePoweredByFooter = true,
        });

        var settings = await svc.GetSettingsAsync();

        Assert.Equal("Acme Identity", settings.CompanyName);
        Assert.Equal("https://acme.example/logo.png", settings.LogoUrl);
        Assert.Equal("support@acme.example", settings.SupportEmail);
        Assert.True(settings.HidePoweredByFooter);
    }
}
