using AuthManager.Core.Options;
using AuthManager.UI.Components;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Server;
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
    /// Call this after UseAuthentication() and UseAuthorization().
    /// </summary>
    /// <example>
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.MapAuthManager();
    /// // Navigate to /authmanager
    /// </example>
    public static WebApplication MapAuthManager(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<AuthManagerOptions>>().Value;
        var prefix = options.RoutePrefix.Trim('/');

        // 1. Serve static web assets from the RCL (CSS, JS, fonts)
        //    This registers the _content/DotNetAuthManager.UI/* path automatically
        app.UseStaticFiles();

        // 2. Map Blazor's SignalR hub scoped to the authmanager path prefix
        //    Keeps the hub isolated from the host app's own Blazor setup
        app.MapBlazorHub($"/{prefix}/_blazor");

        // 3. Map Razor components — AuthManagerApp is the root component.
        //    AddAdditionalAssemblies ensures all @page routes in AuthManager.UI are discovered.
        var componentBuilder = app.MapRazorComponents<AuthManagerApp>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(typeof(AuthManagerApp).Assembly);

        // 4. Apply authorization policy if configured
        if (options.RequireAuthentication)
        {
            if (options.AdminRoles.Length > 0)
                componentBuilder.RequireAuthorization(p => p.RequireRole(options.AdminRoles));
            else
                componentBuilder.RequireAuthorization();
        }

        // 5. Map a simple redirect so GET /authmanager goes to the right place
        app.MapGet($"/{prefix}", (HttpContext ctx) =>
        {
            ctx.Response.Redirect($"/{prefix}/");
            return Task.CompletedTask;
        }).ExcludeFromDescription();

        // 6. Map the JSON health / info endpoint under /{prefix}/api/
        var api = app.MapGroup($"/{prefix}/api");
        MapApiEndpoints(api);

        return app;
    }

    private static void MapApiEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("health", () => Results.Ok(new
        {
            Status = "Healthy",
            Service = "DotNetAuthManager",
            Version = typeof(WebApplicationExtensions).Assembly.GetName().Version?.ToString(),
            Timestamp = DateTimeOffset.UtcNow
        }))
        .WithName("AuthManager.Health")
        .WithTags("AuthManager")
        .ExcludeFromDescription();
    }
}
