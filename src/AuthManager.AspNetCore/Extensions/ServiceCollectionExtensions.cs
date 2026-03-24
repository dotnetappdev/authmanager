using AuthManager.Core.Options;
using AuthManager.Core.Services;
using AuthManager.AspNetCore.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuthManager.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring AuthManager in the DI container.
/// Works just like .NET Aspire's dashboard — call AddAuthManager() and MapAuthManager(), nothing else needed.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AuthManager to the application. Call this after AddIdentity().
    /// </summary>
    /// <typeparam name="TUser">Your IdentityUser type.</typeparam>
    /// <example>
    /// builder.Services.AddIdentity&lt;ApplicationUser, IdentityRole&gt;().AddEntityFrameworkStores&lt;AppDbContext&gt;();
    /// builder.Services.AddAuthManager&lt;ApplicationUser&gt;(options =>
    /// {
    ///     options.RoutePrefix = "authmanager";
    ///     options.DefaultTheme = AuthManagerTheme.Dark;
    ///     options.AdminRoles = ["Admin"];
    /// });
    /// </example>
    public static IServiceCollection AddAuthManager<TUser>(
        this IServiceCollection services,
        Action<AuthManagerOptions>? configure = null)
        where TUser : IdentityUser, new()
        => services.AddAuthManager<TUser, IdentityRole>(configure);

    /// <summary>
    /// Adds AuthManager with a custom role type.
    /// </summary>
    public static IServiceCollection AddAuthManager<TUser, TRole>(
        this IServiceCollection services,
        Action<AuthManagerOptions>? configure = null)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
    {
        // Register options
        var optionsBuilder = services.AddOptions<AuthManagerOptions>();
        if (configure != null)
            optionsBuilder.Configure(configure);

        // Core singleton services
        services.TryAddSingleton<LogAggregationService>();
        services.TryAddSingleton<ILogAggregationService>(sp => sp.GetRequiredService<LogAggregationService>());
        services.TryAddSingleton<IAuditService, InMemoryAuditService>();

        // Scoped services (per Blazor circuit)
        services.TryAddScoped<IUserManagementService, UserManagementService<TUser>>();
        services.TryAddScoped<IRoleManagementService, RoleManagementService<TRole>>();
        services.TryAddScoped<IOAuthProviderService, OAuthProviderService>();
        services.TryAddScoped<IJwtConfigService, JwtConfigService>();

        // Register Blazor infrastructure — the user does NOT need to call AddRazorComponents()
        // This is idempotent; if the user already called it, this is a no-op.
        services.AddRazorComponents()
                .AddInteractiveServerComponents();

        // Register HttpContextAccessor for IP tracking in audit log
        services.AddHttpContextAccessor();

        return services;
    }
}
