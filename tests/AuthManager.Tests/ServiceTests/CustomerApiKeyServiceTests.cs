using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class CustomerApiKeyServiceTests : ServiceTestBase
{
    private static async Task<CustomerDto> CreateCustomerAsync(ICustomerService svc)
    {
        var (_, _, customer) = await svc.CreateCustomerAsync(new CreateCustomerDto { Name = "Acme", Email = "a@acme.test" });
        return customer!;
    }

    [Fact]
    public async Task CreateKeyAsync_returns_a_key_that_validates()
    {
        using var scope = CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var keys = scope.ServiceProvider.GetRequiredService<ICustomerApiKeyService>();
        var customer = await CreateCustomerAsync(customers);

        var (ok, errors, result) = await keys.CreateKeyAsync(new CreateCustomerApiKeyDto
        {
            CustomerId = customer.Id, Name = "Prod key", Scopes = ["read:orders"]
        });

        Assert.True(ok);
        Assert.Empty(errors);
        Assert.StartsWith("ck_live_", result!.ApiKey);

        var validated = await keys.ValidateKeyAsync(result.ApiKey);
        Assert.NotNull(validated);
        Assert.Equal(customer.Id, validated!.CustomerId);
        Assert.Contains("read:orders", validated.Scopes);
    }

    [Fact]
    public async Task ValidateKeyAsync_rejects_a_wrong_key()
    {
        using var scope = CreateScope();
        var keys = scope.ServiceProvider.GetRequiredService<ICustomerApiKeyService>();

        var validated = await keys.ValidateKeyAsync("ck_live_not-a-real-key");

        Assert.Null(validated);
    }

    [Fact]
    public async Task RevokeKeyAsync_makes_validation_fail()
    {
        using var scope = CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var keys = scope.ServiceProvider.GetRequiredService<ICustomerApiKeyService>();
        var customer = await CreateCustomerAsync(customers);
        var (_, _, result) = await keys.CreateKeyAsync(new CreateCustomerApiKeyDto { CustomerId = customer.Id, Name = "K" });

        var (ok, _) = await keys.RevokeKeyAsync(result!.Key.Id);

        Assert.True(ok);
        Assert.Null(await keys.ValidateKeyAsync(result.ApiKey));
    }

    [Fact]
    public async Task An_expired_key_fails_validation()
    {
        using var scope = CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var keys = scope.ServiceProvider.GetRequiredService<ICustomerApiKeyService>();
        var customer = await CreateCustomerAsync(customers);
        var (_, _, result) = await keys.CreateKeyAsync(new CreateCustomerApiKeyDto
        {
            CustomerId = customer.Id, Name = "K", ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
        });

        Assert.Null(await keys.ValidateKeyAsync(result!.ApiKey));
    }

    [Fact]
    public async Task RegenerateKeyAsync_invalidates_the_previous_key()
    {
        using var scope = CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var keys = scope.ServiceProvider.GetRequiredService<ICustomerApiKeyService>();
        var customer = await CreateCustomerAsync(customers);
        var (_, _, created) = await keys.CreateKeyAsync(new CreateCustomerApiKeyDto { CustomerId = customer.Id, Name = "K" });

        var (ok, _, newKey) = await keys.RegenerateKeyAsync(created!.Key.Id);

        Assert.True(ok);
        Assert.Null(await keys.ValidateKeyAsync(created.ApiKey));
        Assert.NotNull(await keys.ValidateKeyAsync(newKey!));
    }
}
