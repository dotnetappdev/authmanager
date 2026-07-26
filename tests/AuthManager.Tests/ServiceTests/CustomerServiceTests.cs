using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class CustomerServiceTests : ServiceTestBase
{
    [Fact]
    public async Task CreateCustomerAsync_then_GetCustomerAsync_round_trips()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICustomerService>();

        var (ok, errors, customer) = await svc.CreateCustomerAsync(new CreateCustomerDto
        {
            Name = "Acme Corp", Email = "billing@acme.test", CompanyName = "Acme Corp Ltd"
        });

        Assert.True(ok);
        Assert.Empty(errors);
        var fetched = await svc.GetCustomerAsync(customer!.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Acme Corp", fetched!.Name);
    }

    [Fact]
    public async Task CreateCustomerAsync_rejects_a_missing_email()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICustomerService>();

        var (ok, errors, _) = await svc.CreateCustomerAsync(new CreateCustomerDto { Name = "No Email" });

        Assert.False(ok);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task GetCustomersAsync_search_filters_by_name_email_or_company()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        await svc.CreateCustomerAsync(new CreateCustomerDto { Name = "Acme Corp", Email = "a@acme.test" });
        await svc.CreateCustomerAsync(new CreateCustomerDto { Name = "Globex", Email = "b@globex.test" });

        var results = await svc.GetCustomersAsync("acme");

        Assert.Single(results);
        Assert.Equal("Acme Corp", results[0].Name);
    }

    [Fact]
    public async Task UpdateCustomerAsync_changes_fields()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var (_, _, customer) = await svc.CreateCustomerAsync(new CreateCustomerDto { Name = "Old", Email = "old@test.com" });

        var (ok, errors) = await svc.UpdateCustomerAsync(customer!.Id, new UpdateCustomerDto { Name = "New", Email = "new@test.com" });

        Assert.True(ok);
        Assert.Empty(errors);
        var fetched = await svc.GetCustomerAsync(customer.Id);
        Assert.Equal("New", fetched!.Name);
        Assert.Equal("new@test.com", fetched.Email);
    }

    [Fact]
    public async Task DeleteCustomerAsync_removes_it()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var (_, _, customer) = await svc.CreateCustomerAsync(new CreateCustomerDto { Name = "Gone", Email = "gone@test.com" });

        var (ok, _) = await svc.DeleteCustomerAsync(customer!.Id);

        Assert.True(ok);
        Assert.Null(await svc.GetCustomerAsync(customer.Id));
    }
}
