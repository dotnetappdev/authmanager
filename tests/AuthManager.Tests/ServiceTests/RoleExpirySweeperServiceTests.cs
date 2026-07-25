using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

/// <summary>
/// Exercises the actual background hosted service (not just the claim-manipulation logic
/// it shares with <c>MakeRoleAssignmentPermanentAsync</c>) — starts it for real and lets it
/// run its first sweep pass.
/// </summary>
public sealed class RoleExpirySweeperServiceTests : ServiceTestBase
{
    [Fact]
    public async Task The_sweeper_is_registered_as_a_hosted_service()
    {
        using var scope = CreateScope();
        var hostedServices = scope.ServiceProvider.GetServices<IHostedService>();

        Assert.Contains(hostedServices, s => s.GetType().Name.StartsWith("RoleExpirySweeperService"));
    }

    [Fact]
    public async Task An_expired_temporary_role_is_revoked_once_the_sweeper_runs()
    {
        using var scope = CreateScope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userSvc = sp.GetRequiredService<IUserManagementService>();

        var bob = new IdentityUser { UserName = "bob", Email = "bob@example.com" };
        await users.CreateAsync(bob, "Passw0rd!123");
        await roles.CreateAsync(new IdentityRole("Manager"));
        await userSvc.AssignTemporaryRoleAsync(bob.Id, "Manager", DateTimeOffset.UtcNow.AddMilliseconds(50));
        await Task.Delay(150); // let the grant actually expire

        var sweeper = scope.ServiceProvider.GetServices<IHostedService>()
            .First(s => s.GetType().Name.StartsWith("RoleExpirySweeperService"));
        await sweeper.StartAsync(CancellationToken.None);
        await Task.Delay(500); // let the first (immediate) sweep pass complete
        await sweeper.StopAsync(CancellationToken.None);

        // Fresh scope/UserManager — a real host gets one per request too, and this avoids
        // reading stale change-tracked state from the scope that granted the role.
        using var freshScope = CreateScope();
        var freshUsers = freshScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var freshBob = await freshUsers.FindByIdAsync(bob.Id);
        Assert.False(await freshUsers.IsInRoleAsync(freshBob!, "Manager"));

        var freshUserSvc = freshScope.ServiceProvider.GetRequiredService<IUserManagementService>();
        Assert.False((await freshUserSvc.GetRoleExpiriesAsync(bob.Id)).ContainsKey("Manager"));
    }

    [Fact]
    public async Task A_permanent_role_is_left_untouched_by_the_sweeper()
    {
        using var scope = CreateScope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userSvc = sp.GetRequiredService<IUserManagementService>();

        var alice = new IdentityUser { UserName = "alice", Email = "alice@example.com" };
        await users.CreateAsync(alice, "Passw0rd!123");
        await roles.CreateAsync(new IdentityRole("Manager"));
        await userSvc.AssignTemporaryRoleAsync(alice.Id, "Manager", DateTimeOffset.UtcNow.AddMilliseconds(50));
        await userSvc.MakeRoleAssignmentPermanentAsync(alice.Id, "Manager");
        await Task.Delay(150);

        var sweeper = scope.ServiceProvider.GetServices<IHostedService>()
            .First(s => s.GetType().Name.StartsWith("RoleExpirySweeperService"));
        await sweeper.StartAsync(CancellationToken.None);
        await Task.Delay(500);
        await sweeper.StopAsync(CancellationToken.None);

        using var freshScope = CreateScope();
        var freshUsers = freshScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var freshAlice = await freshUsers.FindByIdAsync(alice.Id);
        Assert.True(await freshUsers.IsInRoleAsync(freshAlice!, "Manager"));
    }
}
