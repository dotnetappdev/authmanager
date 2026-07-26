using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

/// <summary>
/// Passkey registration/sign-in requires a real browser authenticator to produce attestation/
/// assertion responses, so the actual WebAuthn ceremony isn't exercised here (that's covered by
/// manual browser testing — see PasskeysPage.razor). These tests lock in the parts that don't
/// need a real credential: the store wiring (UserManager surfaces AddOrUpdatePasskeyAsync et al.
/// out of the box on .NET 9+), and this service's error handling for bad input.
/// </summary>
public sealed class PasskeyServiceTests : ServiceTestBase
{
    private async Task<IdentityUser> SeedUserAsync(IServiceProvider sp)
    {
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = "alice", Email = "alice@example.com" };
        await users.CreateAsync(user, "Passw0rd!123");
        return user;
    }

    [Fact]
    public void SupportsPasskeys_is_true_out_of_the_box()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPasskeyService>();

        Assert.True(svc.SupportsPasskeys);
    }

    // GetCreationOptionsAsync/GetRequestOptionsAsync for a real user both go through
    // SignInManager, which requires a genuine HttpContext (it correlates the ceremony's
    // challenge state to the current request) — that doesn't exist in this bare-DI-scope
    // test harness. They're covered at the HTTP level instead, in PasskeysApiTests, where
    // WebApplicationFactory provides a real one.

    [Fact]
    public async Task GetCreationOptionsAsync_returns_null_for_an_unknown_user()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IPasskeyService>();

        var options = await svc.GetCreationOptionsAsync("no-such-user-id");

        Assert.Null(options);
    }

    [Fact]
    public async Task A_fresh_user_has_no_passkeys()
    {
        using var scope = CreateScope();
        var user = await SeedUserAsync(scope.ServiceProvider);
        var svc = scope.ServiceProvider.GetRequiredService<IPasskeyService>();

        var passkeys = await svc.GetPasskeysAsync(user.Id);

        Assert.Empty(passkeys);
    }

    [Fact]
    public async Task RemovePasskeyAsync_rejects_an_invalid_credential_id()
    {
        using var scope = CreateScope();
        var user = await SeedUserAsync(scope.ServiceProvider);
        var svc = scope.ServiceProvider.GetRequiredService<IPasskeyService>();

        var (ok, errors) = await svc.RemovePasskeyAsync(user.Id, "not-valid-base64!!");

        Assert.False(ok);
        Assert.NotEmpty(errors);
    }
}
