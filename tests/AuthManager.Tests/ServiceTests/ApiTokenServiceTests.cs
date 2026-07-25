using AuthManager.Core.Models;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

public sealed class ApiTokenServiceTests : ServiceTestBase
{
    private async Task<(IApiTokenService Svc, IdentityUser User)> SeedAsync(IServiceProvider sp)
    {
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = "alice", Email = "alice@example.com" };
        await users.CreateAsync(user, "Passw0rd!123");
        return (sp.GetRequiredService<IApiTokenService>(), user);
    }

    [Fact]
    public async Task A_created_token_validates_and_the_raw_value_is_only_returned_once()
    {
        using var scope = CreateScope();
        var (svc, user) = await SeedAsync(scope.ServiceProvider);

        var (ok, _, result) = await svc.CreateTokenAsync(new CreateApiTokenDto { Name = "CI", UserId = user.Id });

        Assert.True(ok);
        Assert.NotNull(result);
        Assert.StartsWith("am_", result!.RawToken);

        var validated = await svc.ValidateTokenAsync(result.RawToken);
        Assert.NotNull(validated);
        Assert.Equal(user.Id, validated!.UserId);
    }

    [Fact]
    public async Task ValidateTokenAsync_rejects_a_wrong_token()
    {
        using var scope = CreateScope();
        var (svc, user) = await SeedAsync(scope.ServiceProvider);
        await svc.CreateTokenAsync(new CreateApiTokenDto { Name = "CI", UserId = user.Id });

        var validated = await svc.ValidateTokenAsync("am_totally-not-a-real-token");

        Assert.Null(validated);
    }

    [Fact]
    public async Task A_revoked_token_no_longer_validates()
    {
        using var scope = CreateScope();
        var (svc, user) = await SeedAsync(scope.ServiceProvider);
        var (_, _, result) = await svc.CreateTokenAsync(new CreateApiTokenDto { Name = "CI", UserId = user.Id });

        var (revokeOk, _) = await svc.RevokeTokenAsync(result!.Token.Id);
        var validated = await svc.ValidateTokenAsync(result.RawToken);

        Assert.True(revokeOk);
        Assert.Null(validated);
    }

    [Fact]
    public async Task An_expired_token_does_not_validate()
    {
        using var scope = CreateScope();
        var (svc, user) = await SeedAsync(scope.ServiceProvider);

        var (_, _, result) = await svc.CreateTokenAsync(new CreateApiTokenDto
        {
            Name = "Short-lived",
            UserId = user.Id,
            ExpiresInDays = -1 // already expired
        });

        var validated = await svc.ValidateTokenAsync(result!.RawToken);

        Assert.Null(validated);
    }

    [Fact]
    public async Task DeleteTokenAsync_removes_it_from_the_list()
    {
        using var scope = CreateScope();
        var (svc, user) = await SeedAsync(scope.ServiceProvider);
        var (_, _, result) = await svc.CreateTokenAsync(new CreateApiTokenDto { Name = "CI", UserId = user.Id });

        var (ok, _) = await svc.DeleteTokenAsync(result!.Token.Id);

        Assert.True(ok);
        Assert.Empty(await svc.GetTokensAsync(user.Id));
    }
}
