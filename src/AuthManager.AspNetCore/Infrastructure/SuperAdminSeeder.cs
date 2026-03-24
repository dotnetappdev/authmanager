using AuthManager.Core.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Infrastructure;

/// <summary>
/// Seeds the SuperAdmin role and a default SuperAdmin user on application startup.
/// Only runs when <see cref="AuthManagerOptions.SeedSuperAdmin"/> is true.
///
/// WARNING: this creates a real user with a default password.
/// Change the password immediately after the first login.
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

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var opts = scope.ServiceProvider.GetRequiredService<IOptions<AuthManagerOptions>>().Value;

        if (!opts.SeedSuperAdmin) return;

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<TRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        // ── Ensure SuperAdmin role ──────────────────────────────────────────
        var roleName = opts.SuperAdminRole;
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            _logger.LogWarning(
                "[DotNetAuthManager] SeedSuperAdmin: role '{Role}' does not exist — creating it now. " +
                "Disable options.SeedSuperAdmin after initial setup.",
                roleName);

            var role = new TRole();
            role.Name = roleName;       // TRole might not expose Name as init-only
            await roleManager.SetRoleNameAsync(role, roleName);
            var createRole = await roleManager.CreateAsync(role);
            if (!createRole.Succeeded)
            {
                _logger.LogError("[DotNetAuthManager] Failed to create '{Role}': {Errors}",
                    roleName, string.Join(", ", createRole.Errors.Select(e => e.Description)));
                return;
            }
        }

        // ── Ensure SuperAdmin user ─────────────────────────────────────────
        var email = opts.SeedSuperAdminEmail;
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is null)
        {
            _logger.LogWarning(
                "[DotNetAuthManager] SeedSuperAdmin: creating default SuperAdmin user '{Email}'. " +
                "⚠️  Change the password immediately after first login! " +
                "Set options.SeedSuperAdmin = false once set up.",
                email);

            var user = new TUser
            {
                UserName = email.Split('@')[0],    // derive username from email local-part
                Email = email,
                EmailConfirmed = true
            };

            var createUser = await userManager.CreateAsync(user, opts.SeedSuperAdminPassword);
            if (!createUser.Succeeded)
            {
                _logger.LogError("[DotNetAuthManager] Failed to create SuperAdmin user: {Errors}",
                    string.Join(", ", createUser.Errors.Select(e => e.Description)));
                return;
            }

            await userManager.AddToRoleAsync(user, roleName);

            _logger.LogWarning(
                "[DotNetAuthManager] SuperAdmin seeded — email: '{Email}', role: '{Role}'. " +
                "Login at /{Prefix} and change the password now.",
                email, roleName, opts.RoutePrefix);
        }
        else if (!await userManager.IsInRoleAsync(existing, roleName))
        {
            // User exists but lost the role — re-assign
            _logger.LogWarning(
                "[DotNetAuthManager] SeedSuperAdmin: user '{Email}' exists but is missing role '{Role}'. Re-adding.",
                email, roleName);
            await userManager.AddToRoleAsync(existing, roleName);
        }
        else
        {
            _logger.LogInformation(
                "[DotNetAuthManager] SuperAdmin '{Email}' is already set up — seeding skipped.",
                email);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
