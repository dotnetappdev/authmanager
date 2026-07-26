using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class SubscriptionServiceTests : ServiceTestBase
{
    private static async Task<CustomerDto> CreateCustomerAsync(ICustomerService svc)
    {
        var (_, _, customer) = await svc.CreateCustomerAsync(new CreateCustomerDto { Name = "Acme", Email = "a@acme.test" });
        return customer!;
    }

    [Fact]
    public async Task CreatePlanAsync_rejects_a_duplicate_name()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        await svc.CreatePlanAsync(new CreateSubscriptionPlanDto { Name = "Pro", PriceCents = 999 });

        var (ok, errors, _) = await svc.CreatePlanAsync(new CreateSubscriptionPlanDto { Name = "Pro", PriceCents = 1999 });

        Assert.False(ok);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task SubscribeAsync_with_a_trial_starts_in_Trialing()
    {
        using var scope = CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var subs = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        var customer = await CreateCustomerAsync(customers);
        var (_, _, plan) = await subs.CreatePlanAsync(new CreateSubscriptionPlanDto { Name = "Pro", PriceCents = 999, TrialDays = 14 });

        var (ok, errors, subscription) = await subs.SubscribeAsync(new CreateCustomerSubscriptionDto
        {
            CustomerId = customer.Id, PlanId = plan!.Id
        });

        Assert.True(ok);
        Assert.Empty(errors);
        Assert.Equal(SubscriptionStatus.Trialing, subscription!.Status);
        Assert.NotNull(subscription.TrialEndsAt);
    }

    [Fact]
    public async Task SubscribeAsync_with_SkipTrial_starts_Active()
    {
        using var scope = CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var subs = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        var customer = await CreateCustomerAsync(customers);
        var (_, _, plan) = await subs.CreatePlanAsync(new CreateSubscriptionPlanDto { Name = "Pro", PriceCents = 999, TrialDays = 14 });

        var (ok, _, subscription) = await subs.SubscribeAsync(new CreateCustomerSubscriptionDto
        {
            CustomerId = customer.Id, PlanId = plan!.Id, SkipTrial = true
        });

        Assert.True(ok);
        Assert.Equal(SubscriptionStatus.Active, subscription!.Status);
    }

    [Fact]
    public async Task CancelSubscriptionAsync_then_ReactivateSubscriptionAsync_round_trips()
    {
        using var scope = CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var subs = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        var customer = await CreateCustomerAsync(customers);
        var (_, _, plan) = await subs.CreatePlanAsync(new CreateSubscriptionPlanDto { Name = "Pro", PriceCents = 999 });
        var (_, _, subscription) = await subs.SubscribeAsync(new CreateCustomerSubscriptionDto { CustomerId = customer.Id, PlanId = plan!.Id, SkipTrial = true });

        var (canceled, _) = await subs.CancelSubscriptionAsync(subscription!.Id);
        var afterCancel = await subs.GetSubscriptionAsync(subscription.Id);
        var (reactivated, _) = await subs.ReactivateSubscriptionAsync(subscription.Id);
        var afterReactivate = await subs.GetSubscriptionAsync(subscription.Id);

        Assert.True(canceled);
        Assert.Equal(SubscriptionStatus.Canceled, afterCancel!.Status);
        Assert.True(reactivated);
        Assert.Equal(SubscriptionStatus.Active, afterReactivate!.Status);
    }

    [Fact]
    public async Task DeletePlanAsync_is_rejected_while_a_subscription_references_it()
    {
        using var scope = CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var subs = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        var customer = await CreateCustomerAsync(customers);
        var (_, _, plan) = await subs.CreatePlanAsync(new CreateSubscriptionPlanDto { Name = "Pro", PriceCents = 999 });
        await subs.SubscribeAsync(new CreateCustomerSubscriptionDto { CustomerId = customer.Id, PlanId = plan!.Id, SkipTrial = true });

        var (ok, errors) = await subs.DeletePlanAsync(plan.Id);

        Assert.False(ok);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task GetActiveSubscriptionForCustomerAsync_finds_the_active_one()
    {
        using var scope = CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var subs = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
        var customer = await CreateCustomerAsync(customers);
        var (_, _, plan) = await subs.CreatePlanAsync(new CreateSubscriptionPlanDto { Name = "Pro", PriceCents = 999 });
        await subs.SubscribeAsync(new CreateCustomerSubscriptionDto { CustomerId = customer.Id, PlanId = plan!.Id, SkipTrial = true });

        var active = await subs.GetActiveSubscriptionForCustomerAsync(customer.Id);

        Assert.NotNull(active);
        Assert.Equal(plan.Id, active!.PlanId);
    }
}
