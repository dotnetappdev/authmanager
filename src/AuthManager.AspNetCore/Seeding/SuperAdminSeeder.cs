using AuthManager.Core.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Seeding;

/// <summary>
/// Seeds the SuperAdmin role and a default SuperAdmin user on startup.
/// Only runs when <see cref="AuthManagerOptions.SeedSuperAdmin"/> is true.
///
/// ⚠️  Set SeedSuperAdmin = false once you have logged in and changed the password.
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
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<AuthManagerOptions>>().Value;

        if (!opts.SeedSuperAdmin) return;

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<TRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();
        var roleName    = opts.SuperAdminRole;

        // Ensure role exists
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            _logger.LogWarning(
                "[DotNetAuthManager] Creating '{Role}' role. " +
                "Disable options.SeedSuperAdmin after initial setup.", roleName);

            var role = new TRole();
            await roleManager.SetRoleNameAsync(role, roleName);
            var result = await roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                _logger.LogError("[DotNetAuthManager] Failed to create '{Role}': {Errors}",
                    roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }
        }

        // Ensure user exists
        var email    = opts.SeedSuperAdminEmail;
        var existing = await userManager.FindByEmailAsync(email);

        if (existing is null)
        {
            _logger.LogWarning(
                "[DotNetAuthManager] Creating SuperAdmin user '{Email}'. " +
                "⚠️  Change the password immediately after first login!", email);

            var user = new TUser
            {
                UserName       = email.Split('@')[0],
                Email          = email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, opts.SeedSuperAdminPassword);
            if (!result.Succeeded)
            {
                _logger.LogError("[DotNetAuthManager] Failed to create SuperAdmin user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
                return;
            }

            await userManager.AddToRoleAsync(user, roleName);

            _logger.LogWarning(
                "[DotNetAuthManager] SuperAdmin ready — email: {Email}, role: {Role}. " +
                "Login at /{Prefix} and change the password now.",
                email, roleName, opts.RoutePrefix);
        }
        else if (!await userManager.IsInRoleAsync(existing, roleName))
        {
            _logger.LogWarning(
                "[DotNetAuthManager] User '{Email}' is missing role '{Role}' — re-adding.",
                email, roleName);
            await userManager.AddToRoleAsync(existing, roleName);
        }
        else
        {
            _logger.LogDebug(
                "[DotNetAuthManager] SuperAdmin '{Email}' already set up — skipping seed.", email);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
