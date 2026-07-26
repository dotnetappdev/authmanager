using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

/// <summary>
/// Regression coverage for a real bug: UpdateProviderAsync used to be a no-op (it only logged),
/// so every "Save" on the SSO settings page silently did nothing. These lock in that changes
/// now actually persist, that a blank secret/certificate on save means "keep the existing one"
/// (the UI never re-displays secrets, so a blank field must not overwrite them), and that custom
/// OIDC providers can be added and removed at runtime.
/// </summary>
public sealed class SsoServiceTests : ServiceTestBase
{
    [Fact]
    public async Task UpdateProviderAsync_persists_EntraId_settings_across_calls()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISsoService>();

        var (ok, errors) = await svc.UpdateProviderAsync(new UpdateSsoProviderDto
        {
            Key = "entraid",
            Enabled = true,
            Settings = new Dictionary<string, string>
            {
                ["TenantId"] = "contoso.onmicrosoft.com",
                ["ClientId"] = "client-123",
                ["ClientSecret"] = "super-secret-value",
            }
        });

        Assert.True(ok);
        Assert.Empty(errors);

        var provider = await svc.GetProviderAsync("entraid");
        Assert.NotNull(provider);
        Assert.True(provider!.IsEnabled);
        Assert.Equal("contoso.onmicrosoft.com", provider.Settings["TenantId"]);
    }

    [Fact]
    public async Task UpdateProviderAsync_with_a_blank_secret_keeps_the_previous_one()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISsoService>();

        await svc.UpdateProviderAsync(new UpdateSsoProviderDto
        {
            Key = "entraid",
            Enabled = true,
            Settings = new Dictionary<string, string> { ["ClientSecret"] = "original-secret" }
        });

        // Simulate the UI re-saving without touching the (never re-displayed) secret field.
        await svc.UpdateProviderAsync(new UpdateSsoProviderDto
        {
            Key = "entraid",
            Enabled = true,
            Settings = new Dictionary<string, string> { ["ClientSecret"] = "", ["TenantId"] = "updated-tenant" }
        });

        var provider = await svc.GetProviderAsync("entraid");
        Assert.Equal("updated-tenant", provider!.Settings["TenantId"]);
        // The masked secret preview should reflect the ORIGINAL secret's length/shape, not be empty.
        Assert.NotEqual(string.Empty, provider.Settings["ClientSecret"]);
    }

    [Fact]
    public async Task CreateOidcProviderAsync_adds_a_provider_that_shows_up_in_the_list()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISsoService>();

        var (ok, errors) = await svc.CreateOidcProviderAsync(new CreateOidcProviderDto
        {
            Name = "okta",
            DisplayName = "Okta",
            Authority = "https://dev-123.okta.com/oauth2/default",
            ClientId = "abc123",
            ClientSecret = "secret"
        });

        Assert.True(ok);
        Assert.Empty(errors);

        var providers = await svc.GetProvidersAsync();
        var okta = providers.SingleOrDefault(p => p.Key == "oidc:okta");
        Assert.NotNull(okta);
        Assert.Equal("Okta", okta!.DisplayName);
        Assert.True(okta.IsEnabled); // new providers start enabled
    }

    [Fact]
    public async Task CreateOidcProviderAsync_rejects_a_duplicate_name()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISsoService>();
        await svc.CreateOidcProviderAsync(new CreateOidcProviderDto { Name = "okta", DisplayName = "Okta" });

        var (ok, errors) = await svc.CreateOidcProviderAsync(new CreateOidcProviderDto { Name = "okta", DisplayName = "Okta Again" });

        Assert.False(ok);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task DeleteOidcProviderAsync_removes_it_from_the_list()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISsoService>();
        await svc.CreateOidcProviderAsync(new CreateOidcProviderDto { Name = "okta", DisplayName = "Okta" });

        var (ok, _) = await svc.DeleteOidcProviderAsync("okta");

        Assert.True(ok);
        var providers = await svc.GetProvidersAsync();
        Assert.DoesNotContain(providers, p => p.Key == "oidc:okta");
    }

    [Fact]
    public async Task UpdateProviderAsync_for_Saml_persists_the_uploaded_certificate()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISsoService>();

        var (ok, _) = await svc.UpdateProviderAsync(new UpdateSsoProviderDto
        {
            Key = "saml",
            Enabled = true,
            Settings = new Dictionary<string, string>
            {
                ["ServiceProviderEntityId"] = "https://myapp.example.com",
                ["IdentityProviderSsoUrl"] = "https://idp.example.com/sso",
                ["IdentityProviderCertificate"] = Convert.ToBase64String("fake-cert-bytes"u8.ToArray()),
            }
        });

        Assert.True(ok);
        var provider = await svc.GetProviderAsync("saml");
        Assert.Equal("✓ configured", provider!.Settings["IdP Certificate"]);
    }
}
