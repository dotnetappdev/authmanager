using AuthManager.AspNetCore.Data;
using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.Infrastructure;

/// <summary>
/// Minimal Identity store for tests — mirrors what a real host app provides.
/// </summary>
public sealed class TestAppDbContext : IdentityDbContext<IdentityUser>
{
    public TestAppDbContext(DbContextOptions<TestAppDbContext> options) : base(options) { }
}

/// <summary>
/// Base class for service-level tests. Builds a real DI container — the same
/// <c>AddAuthManager&lt;TUser&gt;()</c> call a host app would make — backed by two
/// throwaway SQLite files (one for Identity, one for AuthManager's internal store) that
/// are created fresh in <see cref="InitializeAsync"/> and deleted in <see cref="DisposeAsync"/>.
/// Each test method gets its own instance (xUnit creates a new test class per test), so
/// each test gets a fully isolated database — no shared state, no ordering dependencies.
/// </summary>
public abstract class ServiceTestBase : IAsyncLifetime
{
    private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"amtest-{Guid.NewGuid():N}.db");
    private readonly string _authDbFile = Path.Combine(Path.GetTempPath(), $"amtest-auth-{Guid.NewGuid():N}.db");

    protected WebApplication App { get; private set; } = default!;

    /// <summary>Override to customize <c>AddAuthManager()</c> options for a specific test class.</summary>
    protected virtual void ConfigureOptions(AuthManagerOptions options) { }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDbContext<TestAppDbContext>(o => o.UseSqlite($"Data Source={_dbFile}"));
        builder.Services
            .AddIdentity<IdentityUser, IdentityRole>()
            .AddEntityFrameworkStores<TestAppDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.AddAuthManager<IdentityUser>(options =>
        {
            options.InternalDatabaseConnectionString = $"Data Source={_authDbFile}";
            ConfigureOptions(options);
        });

        App = builder.Build();

        using (var scope = App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestAppDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        using (var scope = App.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuthManagerDbContext>>();
            await using var authDb = await factory.CreateDbContextAsync();
            await authDb.Database.EnsureCreatedAsync();
        }
    }

    public async Task DisposeAsync()
    {
        await App.DisposeAsync();
        DeleteSqliteFiles(_dbFile);
        DeleteSqliteFiles(_authDbFile);
    }

    private static void DeleteSqliteFiles(string path)
    {
        foreach (var candidate in new[] { path, path + "-journal", path + "-wal", path + "-shm" })
        {
            try { if (File.Exists(candidate)) File.Delete(candidate); }
            catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>Creates a fresh DI scope — mirrors a real per-request scope in a host app.</summary>
    protected IServiceScope CreateScope() => App.Services.CreateScope();
}
