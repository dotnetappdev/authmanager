using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class UserManagementServiceTests : ServiceTestBase
{
    private async Task<(UserManager<IdentityUser> Users, RoleManager<IdentityRole> Roles, IUserManagementService Svc, IdentityUser User)> SeedAsync(IServiceProvider sp)
    {
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var svc = sp.GetRequiredService<IUserManagementService>();

        var user = new IdentityUser { UserName = "bob", Email = "bob@example.com" };
        await users.CreateAsync(user, "Passw0rd!123");

        return (users, roles, svc, user);
    }

    [Fact]
    public async Task CreateUserAsync_creates_a_user_with_roles_and_claims()
    {
        using var scope = CreateScope();
        var sp = scope.ServiceProvider;
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var svc = sp.GetRequiredService<IUserManagementService>();
        await roles.CreateAsync(new IdentityRole("Editor"));

        var (ok, errors) = await svc.CreateUserAsync(new CreateUserDto
        {
            UserName = "newbie",
            Email = "newbie@example.com",
            Password = "Passw0rd!123",
            Roles = ["Editor"],
            Claims = [new ClaimDto("department", "Engineering")]
        });

        Assert.True(ok, string.Join(", ", errors));
        var created = await svc.GetUserByUserNameAsync("newbie");
        Assert.NotNull(created);
        Assert.Contains("Editor", created!.Roles);
        Assert.Contains(created.Claims, c => c.Type == "department" && c.Value == "Engineering");
    }

    [Fact]
    public async Task GenerateRecoveryCodesAsync_requires_two_factor_to_already_be_enabled()
    {
        using var scope = CreateScope();
        var (_, _, svc, user) = await SeedAsync(scope.ServiceProvider);

        var (ok, errors, codes) = await svc.GenerateRecoveryCodesAsync(user.Id);

        Assert.False(ok);
        Assert.Null(codes);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task GenerateRecoveryCodesAsync_returns_the_requested_number_of_codes_once_2fa_is_on()
    {
        using var scope = CreateScope();
        var (users, _, svc, user) = await SeedAsync(scope.ServiceProvider);
        await users.SetTwoFactorEnabledAsync(user, true);

        var (ok, _, codes) = await svc.GenerateRecoveryCodesAsync(user.Id, count: 8);

        Assert.True(ok);
        Assert.Equal(8, codes!.Length);
        Assert.Equal(8, await svc.GetRecoveryCodesRemainingAsync(user.Id));
    }

    [Fact]
    public async Task Regenerating_recovery_codes_invalidates_the_previous_set()
    {
        using var scope = CreateScope();
        var (users, _, svc, user) = await SeedAsync(scope.ServiceProvider);
        await users.SetTwoFactorEnabledAsync(user, true);

        var (_, _, first) = await svc.GenerateRecoveryCodesAsync(user.Id, count: 5);
        var (_, _, second) = await svc.GenerateRecoveryCodesAsync(user.Id, count: 3);

        Assert.Equal(5, first!.Length);
        Assert.Equal(3, second!.Length);
        Assert.Equal(3, await svc.GetRecoveryCodesRemainingAsync(user.Id));
        Assert.Empty(first.Intersect(second));
    }

    [Fact]
    public async Task AssignTemporaryRoleAsync_grants_the_role_immediately_and_records_its_expiry()
    {
        using var scope = CreateScope();
        var (_, roles, svc, user) = await SeedAsync(scope.ServiceProvider);
        await roles.CreateAsync(new IdentityRole("Manager"));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(1);

        var (ok, _) = await svc.AssignTemporaryRoleAsync(user.Id, "Manager", expiresAt);

        Assert.True(ok);
        var dto = await svc.GetUserByIdAsync(user.Id);
        Assert.Contains("Manager", dto!.Roles);

        var expiries = await svc.GetRoleExpiriesAsync(user.Id);
        Assert.True(expiries.ContainsKey("Manager"));
        Assert.Equal(expiresAt, expiries["Manager"], TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task MakeRoleAssignmentPermanentAsync_clears_the_expiry_but_keeps_the_role()
    {
        using var scope = CreateScope();
        var (_, roles, svc, user) = await SeedAsync(scope.ServiceProvider);
        await roles.CreateAsync(new IdentityRole("Manager"));
        await svc.AssignTemporaryRoleAsync(user.Id, "Manager", DateTimeOffset.UtcNow.AddDays(1));

        var (ok, _) = await svc.MakeRoleAssignmentPermanentAsync(user.Id, "Manager");

        Assert.True(ok);
        var expiries = await svc.GetRoleExpiriesAsync(user.Id);
        Assert.False(expiries.ContainsKey("Manager"));
        var dto = await svc.GetUserByIdAsync(user.Id);
        Assert.Contains("Manager", dto!.Roles);
    }

    [Fact]
    public async Task Required_actions_can_be_added_and_removed()
    {
        using var scope = CreateScope();
        var (_, _, svc, user) = await SeedAsync(scope.ServiceProvider);

        await svc.AddRequiredActionAsync(user.Id, "ConfigureTOTP");
        Assert.Contains("ConfigureTOTP", await svc.GetRequiredActionsAsync(user.Id));

        await svc.RemoveRequiredActionAsync(user.Id, "ConfigureTOTP");
        Assert.DoesNotContain("ConfigureTOTP", await svc.GetRequiredActionsAsync(user.Id));
    }

    [Fact]
    public async Task LockUserAsync_and_UnlockUserAsync_toggle_lockout()
    {
        using var scope = CreateScope();
        var (_, _, svc, user) = await SeedAsync(scope.ServiceProvider);

        await svc.LockUserAsync(user.Id, DateTimeOffset.UtcNow.AddHours(1));
        var locked = await svc.GetUserByIdAsync(user.Id);
        Assert.True(locked!.IsLockedOut);

        await svc.UnlockUserAsync(user.Id);
        var unlocked = await svc.GetUserByIdAsync(user.Id);
        Assert.False(unlocked!.IsLockedOut);
    }

    [Fact]
    public async Task DeleteUserAsync_removes_the_user()
    {
        using var scope = CreateScope();
        var (_, _, svc, user) = await SeedAsync(scope.ServiceProvider);

        var (ok, _) = await svc.DeleteUserAsync(user.Id);

        Assert.True(ok);
        Assert.Null(await svc.GetUserByIdAsync(user.Id));
    }
}
