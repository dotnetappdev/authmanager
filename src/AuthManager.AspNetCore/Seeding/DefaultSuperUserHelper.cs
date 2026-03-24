using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AuthManager.AspNetCore.Seeding;

/// <summary>
/// Shared logic used by both <see cref="SuperAdminSeeder{TUser,TRole}"/> (hosted service)
/// and <c>WebApplication.CreateDefaultSuperUserAsync()</c> (explicit call).
/// </summary>
internal static class DefaultSuperUserHelper
{
    internal static async Task EnsureAsync<TUser, TRole>(
        UserManager<TUser> userManager,
        RoleManager<TRole> roleManager,
        ILogger logger,
        string roleName,
        string email,
        string password,
        string routePrefix)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
    {
        // ── Role ─────────────────────────────────────────────────────────
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            logger.LogWarning(
                "[DotNetAuthManager] Creating '{Role}' role.", roleName);

            var role = new TRole();
            await roleManager.SetRoleNameAsync(role, roleName);
            var roleResult = await roleManager.CreateAsync(role);

            if (!roleResult.Succeeded)
            {
                logger.LogError(
                    "[DotNetAuthManager] Failed to create role '{Role}': {Errors}",
                    roleName, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                return;
            }
        }

        // ── User ─────────────────────────────────────────────────────────
        var existing = await userManager.FindByEmailAsync(email);

        if (existing is null)
        {
            logger.LogWarning(
                "[DotNetAuthManager] Creating default SuperAdmin user '{Email}'. " +
                "⚠️  Change the password immediately after first login!", email);

            var user = new TUser
            {
                UserName       = email.Split('@')[0],
                Email          = email,
                EmailConfirmed = true
            };

            var userResult = await userManager.CreateAsync(user, password);

            if (!userResult.Succeeded)
            {
                logger.LogError(
                    "[DotNetAuthManager] Failed to create SuperAdmin user '{Email}': {Errors}",
                    email, string.Join(", ", userResult.Errors.Select(e => e.Description)));
                return;
            }

            await userManager.AddToRoleAsync(user, roleName);

            logger.LogWarning(
                "[DotNetAuthManager] SuperAdmin ready — login at /{Prefix} with {Email}. " +
                "Change the password now!", routePrefix, email);
        }
        else if (!await userManager.IsInRoleAsync(existing, roleName))
        {
            logger.LogWarning(
                "[DotNetAuthManager] Existing user '{Email}' is missing role '{Role}' — re-adding.",
                email, roleName);
            await userManager.AddToRoleAsync(existing, roleName);
        }
        else
        {
            logger.LogDebug(
                "[DotNetAuthManager] SuperAdmin '{Email}' already exists — skipping.", email);
        }
    }
}
