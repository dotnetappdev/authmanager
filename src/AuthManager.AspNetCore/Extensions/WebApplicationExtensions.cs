using AuthManager.AspNetCore.Seeding;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using AuthManager.UI.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Extensions;

/// <summary>
/// Extension methods for mapping AuthManager in the ASP.NET Core request pipeline.
/// Works like .NET Aspire — a single call handles everything.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Maps the AuthManager UI at the configured route prefix.
    ///
    /// Access is restricted to users who hold the <see cref="AuthManagerOptions.SuperAdminRole"/>
    /// role (default: "SuperAdmin"). Only SuperAdmins can log in to the management UI.
    ///
    /// Call this after UseAuthentication() and UseAuthorization().
    /// </summary>
    /// <example>
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.MapAuthManager();   // → /authmanager, SuperAdmin only
    /// </example>
    public static WebApplication MapAuthManager(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<AuthManagerOptions>>().Value;
        var prefix = options.RoutePrefix.Trim('/');

        // 1. Serve static web assets from the RCL (_content/DotNetAuthManager.UI/*)
        app.UseStaticFiles();

        // 2. Blazor SignalR hub — isolated under the authmanager prefix
        app.MapBlazorHub($"/{prefix}/_blazor");

        // 3. Razor components — AuthManagerApp is the root; discovers all @page routes in the RCL
        var componentBuilder = app.MapRazorComponents<AuthManagerApp>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(typeof(AuthManagerApp).Assembly);

        // 4. Authorization — locked to SuperAdminRole by default.
        //    This is intentionally strict: even authenticated users without the SuperAdmin role
        //    cannot access the management UI.
        if (options.RequireAuthentication)
        {
            componentBuilder.RequireAuthorization(policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(options.SuperAdminRole);
            });
        }

        // 5. Redirect /authmanager → /authmanager/
        app.MapGet($"/{prefix}", (HttpContext ctx) =>
        {
            ctx.Response.Redirect($"/{prefix}/");
            return Task.CompletedTask;
        }).ExcludeFromDescription();

        // 6. Health / info endpoint at /{prefix}/api/health
        var api = app.MapGroup($"/{prefix}/api");
        MapApiEndpoints(api, options);

        return app;
    }

    /// <summary>
    /// Creates the SuperAdmin role and default SuperAdmin user right now, explicitly.
    ///
    /// Use this instead of (or alongside) <c>options.SeedSuperAdmin = true</c> when you
    /// want startup to block until the admin account exists — for example in migrations,
    /// integration tests, or Docker first-run scripts.
    ///
    /// Idempotent — safe to call multiple times.
    /// </summary>
    /// <param name="app">The built <see cref="WebApplication"/>.</param>
    /// <param name="email">Overrides <see cref="AuthManagerOptions.SeedSuperAdminEmail"/>.</param>
    /// <param name="password">Overrides <see cref="AuthManagerOptions.SeedSuperAdminPassword"/>.</param>
    /// <param name="roleName">Overrides <see cref="AuthManagerOptions.SuperAdminRole"/>.</param>
    /// <example>
    /// var app = builder.Build();
    ///
    /// await app.CreateDefaultSuperUserAsync&lt;ApplicationUser&gt;();
    ///
    /// app.MapAuthManager();
    /// app.Run();
    /// </example>
    public static async Task CreateDefaultSuperUserAsync<TUser, TRole>(
        this WebApplication app,
        string? email    = null,
        string? password = null,
        string? roleName = null)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
    {
        using var scope = app.Services.CreateScope();
        var sp          = scope.ServiceProvider;
        var opts        = sp.GetRequiredService<IOptions<AuthManagerOptions>>().Value;
        var logger      = sp.GetRequiredService<ILogger<WebApplication>>();
        var userManager = sp.GetRequiredService<UserManager<TUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<TRole>>();

        await DefaultSuperUserHelper.EnsureAsync(
            userManager, roleManager, logger,
            roleName ?? opts.SuperAdminRole,
            email    ?? opts.SeedSuperAdminEmail,
            password ?? opts.SeedSuperAdminPassword,
            opts.RoutePrefix);
    }

    /// <summary>
    /// Convenience overload — uses <c>IdentityRole</c> as the role type.
    /// </summary>
    public static Task CreateDefaultSuperUserAsync<TUser>(
        this WebApplication app,
        string? email    = null,
        string? password = null,
        string? roleName = null)
        where TUser : IdentityUser, new()
        => app.CreateDefaultSuperUserAsync<TUser, IdentityRole>(email, password, roleName);

    private static void MapApiEndpoints(RouteGroupBuilder api, AuthManagerOptions options)
    {
        // Health check — accessible to SuperAdmin only
        api.MapGet("health", () => Results.Ok(new
        {
            Status = "Healthy",
            Service = "DotNetAuthManager",
            Version = typeof(WebApplicationExtensions).Assembly.GetName().Version?.ToString(),
            Timestamp = DateTimeOffset.UtcNow
        }))
        .RequireAuthorization(p => p.RequireRole(options.SuperAdminRole))
        .WithName("AuthManager.Health")
        .WithTags("AuthManager")
        .ExcludeFromDescription();

        // Redeem impersonation token — navigates to app root on success
        api.MapGet("impersonate/{token}", async (string token, IImpersonationService svc, HttpContext ctx) =>
        {
            var ok = await svc.RedeemTokenAsync(token, ctx);
            return Results.Redirect(ok ? "/" : $"/{options.RoutePrefix}");
        }).AllowAnonymous().ExcludeFromDescription();

        // Exit impersonation — returns admin to the authmanager UI
        api.MapGet("exit-impersonation", async (IImpersonationService svc, HttpContext ctx) =>
        {
            var originalAdmin = ctx.User.FindFirst("am:original_admin")?.Value;
            if (!string.IsNullOrEmpty(originalAdmin))
                await svc.ExitImpersonationAsync(originalAdmin, ctx);
            return Results.Redirect($"/{options.RoutePrefix}");
        }).AllowAnonymous().ExcludeFromDescription();
    }
}
