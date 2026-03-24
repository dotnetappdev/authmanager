using AuthManager.Core.Options;
using AuthManager.Core.Services;
using AuthManager.AspNetCore.Services;
using AuthManager.AspNetCore.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuthManager.AspNetCore.Extensions;

/// <summary>
/// Extension methods for adding DotNetAuthManager to the DI container.
///
/// AuthManager does not manage your database — it works with whatever
/// DbContext + Identity setup you already have. Call AddAuthManager()
/// after your own AddIdentity() call.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AuthManager on top of your existing ASP.NET Identity setup.
    ///
    /// Prerequisites — call these before AddAuthManager():
    ///   builder.Services.AddDbContext&lt;AppDbContext&gt;(...);
    ///   builder.Services.AddIdentity&lt;ApplicationUser, IdentityRole&gt;()
    ///                   .AddEntityFrameworkStores&lt;AppDbContext&gt;();
    /// </summary>
    /// <example>
    /// builder.Services.AddDbContext&lt;AppDbContext&gt;(o => o.UseSqlite("Data Source=app.db"));
    ///
    /// builder.Services.AddIdentity&lt;ApplicationUser, IdentityRole&gt;()
    ///                 .AddEntityFrameworkStores&lt;AppDbContext&gt;()
    ///                 .AddDefaultTokenProviders();
    ///
    /// builder.Services.AddAuthManager&lt;ApplicationUser&gt;(options =>
    /// {
    ///     options.RoutePrefix    = "authmanager";
    ///     options.DefaultTheme   = AuthManagerTheme.Dark;
    ///     options.SeedSuperAdmin = true;
    /// });
    ///
    /// app.MapAuthManager();
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
        var optBuilder = services.AddOptions<AuthManagerOptions>();
        if (configure != null)
            optBuilder.Configure(configure);

        // Core singletons
        services.TryAddSingleton<LogAggregationService>();
        services.TryAddSingleton<ILogAggregationService>(sp => sp.GetRequiredService<LogAggregationService>());
        services.TryAddSingleton<IAuditService, InMemoryAuditService>();
        services.TryAddSingleton<ISessionService, InMemorySessionService>();
        services.TryAddSingleton<ISecurityPolicyService, SecurityPolicyService>();

        // Scoped (one per Blazor circuit)
        services.TryAddScoped<IUserManagementService, UserManagementService<TUser>>();
        services.TryAddScoped<IRoleManagementService, RoleManagementService<TRole>>();
        services.TryAddScoped<IOAuthProviderService, OAuthProviderService>();
        services.TryAddScoped<IJwtConfigService, JwtConfigService>();
        services.TryAddScoped<IUserImportExportService, UserImportExportService<TUser>>();

        // Webhook dispatcher — requires HttpClient
        services.TryAddScoped<IWebhookService, WebhookService>();
        services.AddHttpClient("AuthManager.Webhooks");

        // Blazor — idempotent if host already called AddRazorComponents()
        services.AddRazorComponents()
                .AddInteractiveServerComponents();

        services.AddHttpContextAccessor();

        // Apply PasswordPolicy and SecurityPolicy to ASP.NET Identity options at startup
        services.PostConfigure<Microsoft.AspNetCore.Identity.PasswordOptions>(opts =>
        {
            using var sp = services.BuildServiceProvider();
            var authOpts = sp.GetService<Microsoft.Extensions.Options.IOptions<AuthManagerOptions>>()?.Value;
            if (authOpts is null) return;
            var pp = authOpts.PasswordPolicy;
            opts.RequiredLength         = pp.MinimumLength;
            opts.RequireUppercase       = pp.RequireUppercase;
            opts.RequireLowercase       = pp.RequireLowercase;
            opts.RequireDigit           = pp.RequireDigit;
            opts.RequireNonAlphanumeric = pp.RequireNonAlphanumeric;
        });

        services.PostConfigure<Microsoft.AspNetCore.Identity.LockoutOptions>(opts =>
        {
            using var sp = services.BuildServiceProvider();
            var authOpts = sp.GetService<Microsoft.Extensions.Options.IOptions<AuthManagerOptions>>()?.Value;
            if (authOpts is null) return;
            var sec = authOpts.SecurityPolicy;
            opts.AllowedForNewUsers      = sec.EnableBruteForceDetection;
            opts.MaxFailedAccessAttempts = sec.MaxFailedLoginAttempts;
            opts.DefaultLockoutTimeSpan  = sec.LockoutDuration;
        });

        // Optional SuperAdmin seeder — only acts when options.SeedSuperAdmin = true
        services.AddHostedService<SuperAdminSeeder<TUser, TRole>>();

        return services;
    }
}
