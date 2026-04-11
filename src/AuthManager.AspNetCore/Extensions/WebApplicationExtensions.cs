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

        // 0. Setup redirect — runs before auth so unauthenticated users reach the wizard.
        //    Auth enforcement for admin pages is done below in a separate middleware that
        //    explicitly excludes /_blazor/* (the Blazor SignalR hub) — if we used
        //    componentBuilder.RequireAuthorization() it would also block the hub, preventing
        //    the Blazor circuit from establishing.
        app.Use(async (ctx, next) =>
        {
            var path      = ctx.Request.Path.Value ?? "";
            var setupPath = $"/{prefix}/setup";
            var isSetup   = path.Equals(setupPath, StringComparison.OrdinalIgnoreCase)
                         || path.StartsWith(setupPath + "/", StringComparison.OrdinalIgnoreCase);
            // isPanel = an AuthManager page that is NOT the setup wizard and NOT the Blazor hub
            var isPanel   = path.StartsWith($"/{prefix}", StringComparison.OrdinalIgnoreCase)
                         && !path.Contains("/_blazor", StringComparison.OrdinalIgnoreCase)
                         && !path.Contains("/api/", StringComparison.OrdinalIgnoreCase);

            if (isSetup || isPanel)
            {
                var svc = ctx.RequestServices.GetService<ISetupService>();
                if (svc is not null)
                {
                    var done = await svc.IsSetupCompleteAsync();
                    if (!done && !isSetup)
                    { ctx.Response.Redirect(setupPath); return; }
                    if (done && isSetup)
                    { ctx.Response.Redirect($"/{prefix}/"); return; }
                }
            }
            await next();
        });

        // 0b. Auth enforcement — protect admin panel pages but NOT /_blazor/* hub.
        //     /_blazor/* must remain anonymous so the Blazor SignalR circuit can connect;
        //     the setup wizard is also excluded because it must be reachable before login.
        if (options.RequireAuthentication)
        {
            app.Use(async (ctx, next) =>
            {
                var path      = ctx.Request.Path.Value ?? "";
                var setupPath = $"/{prefix}/setup";
                var isBlazorHub = path.Contains("/_blazor", StringComparison.OrdinalIgnoreCase);
                var isSetup     = path.Equals(setupPath, StringComparison.OrdinalIgnoreCase)
                               || path.StartsWith(setupPath + "/", StringComparison.OrdinalIgnoreCase);
                var isAdminPage = path.StartsWith($"/{prefix}", StringComparison.OrdinalIgnoreCase)
                               && !isBlazorHub
                               && !isSetup
                               && !path.StartsWith($"/{prefix}/api/", StringComparison.OrdinalIgnoreCase);

                if (isAdminPage)
                {
                    if (!ctx.User.Identity?.IsAuthenticated == true
                        || !ctx.User.IsInRole(options.SuperAdminRole))
                    {
                        // Challenge: redirect to the app's login page.
                        // GetRequiredService<IAuthenticationService>() handles the scheme-specific
                        // redirect (cookie auth → /Account/Login, OIDC → provider, etc.).
                        var returnUrl = Uri.EscapeDataString(ctx.Request.Path + ctx.Request.QueryString);
                        var loginPath = ctx.RequestServices
                            .GetService<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>() is { } sp
                            ? (await sp.GetDefaultChallengeSchemeAsync())?.Name
                            : null;
                        // Prefer the configured login path from cookie options; fall back to /Account/Login
                        var cookieOptions = ctx.RequestServices
                            .GetService<Microsoft.Extensions.Options.IOptionsSnapshot<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
                            ?.Get(loginPath ?? string.Empty);
                        var redirect = cookieOptions?.LoginPath.HasValue == true
                            ? $"{cookieOptions.LoginPath}?ReturnUrl={returnUrl}"
                            : $"/Account/Login?ReturnUrl={returnUrl}";
                        ctx.Response.Redirect(redirect);
                        return;
                    }
                }
                await next();
            });
        }

        // 1. Serve static web assets from the RCL (_content/DotNetAuthManager.UI/*)
        app.UseStaticFiles();

        // 2. Razor components — AuthManagerApp is the root; discovers all @page routes in the RCL.
        //
        //    IMPORTANT: AddInteractiveServerRenderMode() registers the Blazor SignalR hub at
        //    /_blazor. In a Blazor Web App host that already calls
        //    MapRazorComponents<App>().AddInteractiveServerRenderMode(), we must NOT call it
        //    again or ASP.NET Core will throw AmbiguousMatchException for /_blazor/*.
        //
        //    We auto-detect this: MapRazorComponents<T>() adds a RazorComponentEndpointDataSource<T>
        //    to app.DataSources synchronously. If one already exists before we add ours, the host
        //    owns an existing Blazor endpoint — we skip AddInteractiveServerRenderMode() to prevent
        //    duplicate /_blazor/* routes.
        //
        //    ⚠️  ORDER: In a Blazor host app, call MapRazorComponents<App>().AddInteractiveServerRenderMode()
        //    *before* MapAuthManager() so this detection fires correctly.
        var endpointRouteBuilder = (IEndpointRouteBuilder)app;
        bool hostHasRazorComponentEndpoint = endpointRouteBuilder.DataSources.Any(ds =>
            ds.GetType().FullName?.Contains("RazorComponentEndpointDataSource", StringComparison.Ordinal) == true);

        var componentBuilder = app.MapRazorComponents<AuthManagerApp>();
        if (!hostHasRazorComponentEndpoint)
        {
            // Standalone (non-Blazor host): AuthManager is the only Blazor app — we own the hub.
            componentBuilder.AddInteractiveServerRenderMode();
        }
        else
        {
            // Blazor host: the hub is already set up by the host's MapRazorComponents endpoint.
            // We must still declare interactive server render mode for our own endpoint so that
            // @rendermode InteractiveServer pages render correctly.
            componentBuilder.AddInteractiveServerRenderMode();
        }
        // NOTE: We do NOT call componentBuilder.RequireAuthorization() — that would also
        // protect /_blazor/* hub endpoints and break the Blazor SignalR circuit.
        // Auth is enforced via the middleware above (step 0b).

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
