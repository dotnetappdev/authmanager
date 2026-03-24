using AuthManager.Core.Options;
using AuthManager.UI.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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
    }
}
