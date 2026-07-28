using AuthManager.AspNetCore.Extensions;
using AuthManager.AspNetCore.Storage;
using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

/// <summary>
/// Proves AuthManagerStorageProvider.SelfContained is a real, working alternative to the
/// default AspNetIdentity provider: no host-provided AddIdentity()/AddEntityFrameworkStores()
/// call at all, yet UserManager/RoleManager — and AuthManager's own services built on top of
/// them — work identically. Each test gets its own throwaway SQLite files, deleted afterwards.
/// </summary>
public sealed class SelfContainedStorageProviderTests : IAsyncLifetime
{
    private readonly string _identityDbFile = Path.Combine(Path.GetTempPath(), $"amtest-selfcontained-{Guid.NewGuid():N}.db");
    private readonly string _authDbFile = Path.Combine(Path.GetTempPath(), $"amtest-selfcontained-auth-{Guid.NewGuid():N}.db");

    private WebApplication _app = default!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();

        // Deliberately no AddIdentity()/AddEntityFrameworkStores() here — that's the point:
        // AuthManager sets up Identity itself when Storage.Provider = SelfContained.
        builder.Services.AddAuthManager<IdentityUser>(options =>
        {
            options.InternalDatabaseConnectionString = $"Data Source={_authDbFile}";
            options.Storage.Provider = AuthManagerStorageProvider.SelfContained;
            options.Storage.ConnectionString = $"Data Source={_identityDbFile}";
            options.Storage.Pbkdf2Iterations = 100_000; // keep tests fast
        });

        _app = builder.Build();

        // Hosted services (schema init) don't run without App.RunAsync() in a test host —
        // create the schema directly, same pattern ServiceTestBase uses for the AspNetIdentity path.
        using var scope = _app.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<SelfContainedIdentityDbContext<IdentityUser, IdentityRole>>();
        await identityDb.Database.EnsureCreatedAsync();

        var authDbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuthManager.AspNetCore.Data.AuthManagerDbContext>>();
        await using var authDb = await authDbFactory.CreateDbContextAsync();
        await authDb.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        foreach (var file in new[] { _identityDbFile, _authDbFile })
        foreach (var candidate in new[] { file, file + "-journal", file + "-wal", file + "-shm" })
        {
            try { if (File.Exists(candidate)) File.Delete(candidate); }
            catch { /* best-effort cleanup */ }
        }
    }

    [Fact]
    public async Task IPasswordHasher_resolves_to_the_self_contained_PBKDF2_hasher()
    {
        using var scope = _app.Services.CreateScope();

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<IdentityUser>>();

        Assert.IsType<SelfContainedPasswordHasher<IdentityUser>>(hasher);
    }

    [Fact]
    public async Task UserManager_creates_a_user_whose_password_is_hashed_with_the_self_contained_hasher()
    {
        using var scope = _app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser { UserName = "alice", Email = "alice@example.com" };
        var result = await users.CreateAsync(user, "Passw0rd!123");

        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(e => e.Description)));

        var stored = await users.FindByNameAsync("alice");
        Assert.NotNull(stored);
        Assert.NotNull(stored!.PasswordHash);

        // The stored hash must be our own format (base64 of: 1-byte marker + 4-byte iteration
        // count + 16-byte salt + 32-byte subkey = 53 bytes), not Identity's built-in format.
        var decoded = Convert.FromBase64String(stored.PasswordHash!);
        Assert.Equal(53, decoded.Length);
        Assert.Equal(0x01, decoded[0]);
    }

    [Fact]
    public async Task UserManager_CheckPasswordAsync_round_trips_through_the_self_contained_store()
    {
        using var scope = _app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        var user = new IdentityUser { UserName = "bob", Email = "bob@example.com" };
        await users.CreateAsync(user, "Passw0rd!123");

        Assert.True(await users.CheckPasswordAsync(user, "Passw0rd!123"));
        Assert.False(await users.CheckPasswordAsync(user, "wrong-password"));
    }

    [Fact]
    public async Task RoleManager_and_role_assignment_work_against_the_self_contained_store()
    {
        using var scope = _app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        await roles.CreateAsync(new IdentityRole("Admin"));
        var user = new IdentityUser { UserName = "carol", Email = "carol@example.com" };
        await users.CreateAsync(user, "Passw0rd!123");
        await users.AddToRoleAsync(user, "Admin");

        Assert.True(await users.IsInRoleAsync(user, "Admin"));
        Assert.Contains("Admin", await users.GetRolesAsync(user));
    }

    [Fact]
    public async Task AuthManagers_own_services_work_unchanged_against_the_self_contained_store()
    {
        // This is the "still use the api for it" guarantee — IUserManagementService (used by
        // the whole /authmanager REST API + Blazor UI) doesn't know or care which storage
        // provider is behind UserManager<TUser>.
        using var scope = _app.Services.CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IUserManagementService>();

        var (ok, errors) = await svc.CreateUserAsync(new CreateUserDto
        {
            UserName = "dave",
            Email = "dave@example.com",
            Password = "Passw0rd!123",
        });

        Assert.True(ok, string.Join(", ", errors));
        var created = await svc.GetUserByUserNameAsync("dave");
        Assert.NotNull(created);
        Assert.Equal("dave@example.com", created!.Email);
    }
}
