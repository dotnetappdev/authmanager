using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class TenantServiceTests : ServiceTestBase
{
    protected override void ConfigureOptions(AuthManagerOptions options)
    {
        options.MultiTenancy.Enabled = true;
        options.MultiTenancy.AllowRootTenant = true;
    }

    private async Task<(UserManager<IdentityUser> Users, ITenantService Tenants, IdentityUser Alice, IdentityUser Bob)> SeedAsync(IServiceProvider sp)
    {
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        var tenants = sp.GetRequiredService<ITenantService>();

        var alice = new IdentityUser { UserName = "alice", Email = "alice@example.com" };
        var bob = new IdentityUser { UserName = "bob", Email = "bob@example.com" };
        await users.CreateAsync(alice, "Passw0rd!123");
        await users.CreateAsync(bob, "Passw0rd!123");

        return (users, tenants, alice, bob);
    }

    [Fact]
    public async Task New_users_start_in_the_root_tenant()
    {
        using var scope = CreateScope();
        var (_, tenants, _, _) = await SeedAsync(scope.ServiceProvider);

        var all = await tenants.GetTenantsAsync();

        var root = Assert.Single(all, t => t.IsRootTenant);
        Assert.Equal(2, root.MemberCount);
    }

    [Fact]
    public async Task CreateTenantAsync_rejects_a_duplicate_id()
    {
        using var scope = CreateScope();
        var (_, tenants, _, _) = await SeedAsync(scope.ServiceProvider);

        var first = await tenants.CreateTenantAsync(new CreateTenantDto { Id = "acme", DisplayName = "Acme" });
        var second = await tenants.CreateTenantAsync(new CreateTenantDto { Id = "acme", DisplayName = "Acme Again" });

        Assert.True(first.Success);
        Assert.False(second.Success);
    }

    [Fact]
    public async Task Assigning_a_user_moves_them_out_of_root_and_updates_counts()
    {
        using var scope = CreateScope();
        var (_, tenants, alice, _) = await SeedAsync(scope.ServiceProvider);
        await tenants.CreateTenantAsync(new CreateTenantDto { Id = "acme", DisplayName = "Acme" });

        var (ok, _) = await tenants.AssignUserToTenantAsync(alice.Id, "acme");

        Assert.True(ok);
        Assert.Equal("acme", await tenants.GetUserTenantIdAsync(alice.Id));

        var all = await tenants.GetTenantsAsync();
        Assert.Equal(1, all.Single(t => t.Id == "acme").MemberCount);
        Assert.Equal(1, all.Single(t => t.IsRootTenant).MemberCount);
    }

    [Fact]
    public async Task Reassigning_a_user_replaces_their_previous_tenant_not_adds_a_second_one()
    {
        using var scope = CreateScope();
        var (_, tenants, alice, _) = await SeedAsync(scope.ServiceProvider);
        await tenants.CreateTenantAsync(new CreateTenantDto { Id = "acme", DisplayName = "Acme" });
        await tenants.CreateTenantAsync(new CreateTenantDto { Id = "beta", DisplayName = "Beta" });

        await tenants.AssignUserToTenantAsync(alice.Id, "acme");
        await tenants.AssignUserToTenantAsync(alice.Id, "beta");

        Assert.Equal("beta", await tenants.GetUserTenantIdAsync(alice.Id));
        var acme = await tenants.GetTenantAsync("acme");
        Assert.Equal(0, acme!.MemberCount);
    }

    [Fact]
    public async Task Deleting_a_tenant_falls_members_back_to_root_without_deleting_them()
    {
        using var scope = CreateScope();
        var (users, tenants, alice, _) = await SeedAsync(scope.ServiceProvider);
        await tenants.CreateTenantAsync(new CreateTenantDto { Id = "acme", DisplayName = "Acme" });
        await tenants.AssignUserToTenantAsync(alice.Id, "acme");

        var (ok, _) = await tenants.DeleteTenantAsync("acme");

        Assert.True(ok);
        Assert.NotNull(await users.FindByIdAsync(alice.Id));
        Assert.Null(await tenants.GetUserTenantIdAsync(alice.Id));
        Assert.DoesNotContain(await tenants.GetTenantsAsync(), t => t.Id == "acme");
    }

    [Fact]
    public async Task The_root_tenant_cannot_be_deleted()
    {
        using var scope = CreateScope();
        var (_, tenants, _, _) = await SeedAsync(scope.ServiceProvider);

        var (ok, errors) = await tenants.DeleteTenantAsync("");

        Assert.False(ok);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task GetTenantMembersAsync_for_root_returns_only_unassigned_users()
    {
        using var scope = CreateScope();
        var (_, tenants, alice, bob) = await SeedAsync(scope.ServiceProvider);
        await tenants.CreateTenantAsync(new CreateTenantDto { Id = "acme", DisplayName = "Acme" });
        await tenants.AssignUserToTenantAsync(alice.Id, "acme");

        var rootMembers = await tenants.GetTenantMembersAsync("");

        var member = Assert.Single(rootMembers);
        Assert.Equal(bob.Id, member.Id);
    }
}
