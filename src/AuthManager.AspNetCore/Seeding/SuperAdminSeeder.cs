using AuthManager.Core.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Seeding;

/// <summary>
/// Automatically seeds the SuperAdmin role and default SuperAdmin user on startup.
/// Only runs when <see cref="AuthManagerOptions.SeedSuperAdmin"/> is <c>true</c>.
///
/// As an alternative to this hosted service you can call
/// <c>app.CreateDefaultSuperUserAsync()</c> explicitly after <c>builder.Build()</c> —
/// that gives you full control over when the seeding happens.
///
/// ⚠️  Set <c>SeedSuperAdmin = false</c> once you have logged in and changed the password.
/// </summary>
public sealed class SuperAdminSeeder<TUser, TRole> : IHostedService
    where TUser : IdentityUser, new()
    where TRole : IdentityRole, new()
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SuperAdminSeeder<TUser, TRole>> _logger;

    public SuperAdminSeeder(IServiceProvider services, ILogger<SuperAdminSeeder<TUser, TRole>> logger)
    {
        _services = services;
        _logger   = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var opts        = scope.ServiceProvider.GetRequiredService<IOptions<AuthManagerOptions>>().Value;

        if (!opts.SeedSuperAdmin) return;

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<TRole>>();

        await DefaultSuperUserHelper.EnsureAsync(
            userManager, roleManager, _logger,
            opts.SuperAdminRole,
            opts.SeedSuperAdminEmail,
            opts.SeedSuperAdminPassword,
            opts.RoutePrefix);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
