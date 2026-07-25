using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class GroupServiceTests : ServiceTestBase
{
    [Fact]
    public async Task Adding_a_user_to_a_group_grants_the_groups_roles()
    {
        using var scope = CreateScope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var groups = sp.GetRequiredService<IGroupService>();

        var user = new IdentityUser { UserName = "alice", Email = "alice@example.com" };
        await users.CreateAsync(user, "Passw0rd!123");
        await roles.CreateAsync(new IdentityRole("Editor"));
        await roles.CreateAsync(new IdentityRole("Viewer"));

        var (createOk, _) = await groups.CreateGroupAsync(new CreateGroupDto
        {
            Name = "Content Team",
            Roles = ["Editor", "Viewer"]
        });
        Assert.True(createOk);

        var group = Assert.Single(await groups.GetGroupsAsync());
        var (addOk, _) = await groups.AddUserToGroupAsync(group.Id, user.Id);

        Assert.True(addOk);
        Assert.True(await users.IsInRoleAsync(user, "Editor"));
        Assert.True(await users.IsInRoleAsync(user, "Viewer"));

        var members = await groups.GetGroupMembersAsync(group.Id);
        Assert.Single(members);

        var userGroups = await groups.GetUserGroupsAsync(user.Id);
        Assert.Single(userGroups);
    }

    [Fact]
    public async Task Removing_a_user_from_a_group_does_not_delete_the_user()
    {
        using var scope = CreateScope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        var groups = sp.GetRequiredService<IGroupService>();

        var user = new IdentityUser { UserName = "bob", Email = "bob@example.com" };
        await users.CreateAsync(user, "Passw0rd!123");
        await groups.CreateGroupAsync(new CreateGroupDto { Name = "Ops" });
        var group = Assert.Single(await groups.GetGroupsAsync());
        await groups.AddUserToGroupAsync(group.Id, user.Id);

        var (ok, _) = await groups.RemoveUserFromGroupAsync(group.Id, user.Id);

        Assert.True(ok);
        Assert.NotNull(await users.FindByIdAsync(user.Id));
        Assert.Empty(await groups.GetGroupMembersAsync(group.Id));
    }

    [Fact]
    public async Task CreateGroupAsync_rejects_a_duplicate_name()
    {
        using var scope = CreateScope();
        var groups = scope.ServiceProvider.GetRequiredService<IGroupService>();

        var first = await groups.CreateGroupAsync(new CreateGroupDto { Name = "Ops" });
        var second = await groups.CreateGroupAsync(new CreateGroupDto { Name = "Ops" });

        Assert.True(first.Success);
        Assert.False(second.Success);
    }

    [Fact]
    public async Task DeleteGroupAsync_removes_the_group_and_its_memberships()
    {
        using var scope = CreateScope();
        var sp = scope.ServiceProvider;
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        var groups = sp.GetRequiredService<IGroupService>();

        var user = new IdentityUser { UserName = "carol", Email = "carol@example.com" };
        await users.CreateAsync(user, "Passw0rd!123");
        await groups.CreateGroupAsync(new CreateGroupDto { Name = "Temp" });
        var group = Assert.Single(await groups.GetGroupsAsync());
        await groups.AddUserToGroupAsync(group.Id, user.Id);

        var (ok, _) = await groups.DeleteGroupAsync(group.Id);

        Assert.True(ok);
        Assert.Empty(await groups.GetGroupsAsync());
    }
}
