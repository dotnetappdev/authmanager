using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class OAuthClientServiceTests : ServiceTestBase
{
    [Fact]
    public async Task CreateClientAsync_returns_a_secret_that_validates()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IOAuthClientService>();

        var (ok, _, result) = await svc.CreateClientAsync(new CreateOAuthClientDto
        {
            ClientId = "billing-service",
            Name = "Billing Service",
            AllowedScopes = ["read:invoices"]
        });

        Assert.True(ok);
        Assert.StartsWith("cs_", result!.ClientSecret);

        var validated = await svc.ValidateClientCredentialsAsync("billing-service", result.ClientSecret);
        Assert.NotNull(validated);
        Assert.Equal("billing-service", validated!.ClientId);
        Assert.Contains("read:invoices", validated.AllowedScopes);
    }

    [Fact]
    public async Task CreateClientAsync_rejects_a_duplicate_client_id()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IOAuthClientService>();
        await svc.CreateClientAsync(new CreateOAuthClientDto { ClientId = "svc-a", Name = "A" });

        var (ok, errors, _) = await svc.CreateClientAsync(new CreateOAuthClientDto { ClientId = "svc-a", Name = "Also A" });

        Assert.False(ok);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task ValidateClientCredentialsAsync_rejects_a_wrong_secret()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IOAuthClientService>();
        await svc.CreateClientAsync(new CreateOAuthClientDto { ClientId = "svc-a", Name = "A" });

        var validated = await svc.ValidateClientCredentialsAsync("svc-a", "not-the-secret");

        Assert.Null(validated);
    }

    [Fact]
    public async Task RegenerateSecretAsync_invalidates_the_previous_secret()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IOAuthClientService>();
        var (_, _, created) = await svc.CreateClientAsync(new CreateOAuthClientDto { ClientId = "svc-a", Name = "A" });

        var (ok, _, newSecret) = await svc.RegenerateSecretAsync(created!.Client.Id);

        Assert.True(ok);
        Assert.NotEqual(created.ClientSecret, newSecret);
        Assert.Null(await svc.ValidateClientCredentialsAsync("svc-a", created.ClientSecret));
        Assert.NotNull(await svc.ValidateClientCredentialsAsync("svc-a", newSecret!));
    }

    [Fact]
    public async Task A_disabled_client_is_rejected_even_with_a_valid_secret()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IOAuthClientService>();
        var (_, _, created) = await svc.CreateClientAsync(new CreateOAuthClientDto { ClientId = "svc-a", Name = "A" });

        var (ok, _) = await svc.UpdateClientAsync(created!.Client.Id, new UpdateOAuthClientDto { Name = "A", Enabled = false });

        Assert.True(ok);
        Assert.Null(await svc.ValidateClientCredentialsAsync("svc-a", created.ClientSecret));
    }

    [Fact]
    public async Task DeleteClientAsync_removes_it()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IOAuthClientService>();
        var (_, _, created) = await svc.CreateClientAsync(new CreateOAuthClientDto { ClientId = "svc-a", Name = "A" });

        var (ok, _) = await svc.DeleteClientAsync(created!.Client.Id);

        Assert.True(ok);
        Assert.Null(await svc.GetClientAsync(created.Client.Id));
        Assert.Null(await svc.ValidateClientCredentialsAsync("svc-a", created.ClientSecret));
    }
}
