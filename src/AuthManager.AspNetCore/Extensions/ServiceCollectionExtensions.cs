using AuthManager.Core.Options;
using AuthManager.Core.Services;
using AuthManager.AspNetCore.Infrastructure;
using AuthManager.AspNetCore.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AuthManager.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring DotNetAuthManager in the DI container.
///
/// Works like .NET Aspire — one or two calls, nothing else needed.
/// The preferred entry point is the IConfiguration overload which reads the
/// connection string from appsettings.json and auto-detects the provider.
/// </summary>
public static class ServiceCollectionExtensions
{
    // =====================================================================
    //  PRIMARY: IConfiguration-based auto-detection
    // =====================================================================

    /// <summary>
    /// Adds AuthManager, reading the connection string from IConfiguration and
    /// auto-detecting the database provider (SQL Server, PostgreSQL, MySQL, SQLite).
    ///
    /// <para>The connection string is read from
    /// <c>ConnectionStrings:{options.ConnectionStringName}</c> (default: "Default").</para>
    ///
    /// <para>Provider detection is based on connection string format. Override with
    /// <c>options.DbProvider = AuthManagerDbProvider.SqlServer</c> if detection fails.</para>
    /// </summary>
    /// <example>
    /// // appsettings.json:
    /// // "ConnectionStrings": { "Default": "Server=.;Database=myapp;Trusted_Connection=True;" }
    ///
    /// builder.Services.AddAuthManager&lt;ApplicationUser&gt;(builder.Configuration, options =>
    /// {
    ///     options.RoutePrefix    = "authmanager";
    ///     options.DefaultTheme   = AuthManagerTheme.Dark;
    ///     options.AdminRoles     = ["SuperAdmin"];
    ///     options.SeedSuperAdmin = true;  // creates SuperAdmin role + user if absent
    /// });
    /// </example>
    public static IServiceCollection AddAuthManager<TUser>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuthManagerOptions>? configure = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        => services.AddAuthManager<TUser, IdentityRole>(configuration, configure, identity);

    /// <summary>
    /// Adds AuthManager with a custom role type, reading from IConfiguration.
    /// </summary>
    public static IServiceCollection AddAuthManager<TUser, TRole>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuthManagerOptions>? configure = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
    {
        // Resolve options early so we can read ConnectionStringName / DbProvider
        var options = new AuthManagerOptions();
        configure?.Invoke(options);

        // Read connection string from configuration
        var connectionString = configuration.GetConnectionString(options.ConnectionStringName);
        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException(
                $"DotNetAuthManager: no connection string named '{options.ConnectionStringName}' found in configuration. " +
                $"Add it to appsettings.json under ConnectionStrings:{options.ConnectionStringName}");

        // Detect or resolve provider
        var detected = options.DbProvider.HasValue
            ? MapExplicit(options.DbProvider.Value)
            : DbProviderDetector.Detect(connectionString);

        if (detected == DbProviderDetector.DetectedProvider.Unknown)
            throw new InvalidOperationException(
                $"DotNetAuthManager: could not detect the database provider from connection string " +
                $"'{options.ConnectionStringName}'. " +
                $"Set options.DbProvider = AuthManagerDbProvider.SqlServer (or PostgreSQL/MySql/Sqlite) to override.");

        var (providerName, nugetPackage) = DbProviderDetector.GetProviderInfo(detected);

        // Register the unified auto DbContext — provider-specific Use{X} called via reflection
        services.AddDbContext<AuthManagerAutoDbContext<TUser, TRole>>(dbOptions =>
        {
            DbProviderDetector.Configure(dbOptions, detected, connectionString);
        });

        // Register Identity against the auto context
        services.AddIdentity<TUser, TRole>(identity ?? (_ => { }))
                .AddEntityFrameworkStores<AuthManagerAutoDbContext<TUser, TRole>>()
                .AddDefaultTokenProviders();

        // Register all AuthManager services (the common path)
        services.AddAuthManagerCore<TUser, TRole>(configure);

        // Log the auto-detected provider on startup
        services.AddSingleton<IHostedService>(sp =>
            new ProviderLogStartup(providerName, options.ConnectionStringName,
                sp.GetRequiredService<ILoggerFactory>()));

        return services;
    }

    // =====================================================================
    //  SECONDARY: explicit connection string (backwards compatible)
    // =====================================================================

    /// <summary>
    /// Adds AuthManager with an explicit connection string.
    /// The provider is still auto-detected from the connection string format.
    /// Use this if you need runtime-computed connection strings.
    /// </summary>
    public static IServiceCollection AddAuthManager<TUser>(
        this IServiceCollection services,
        string connectionString,
        Action<AuthManagerOptions>? configure = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        => services.AddAuthManager<TUser, IdentityRole>(connectionString, configure, identity);

    public static IServiceCollection AddAuthManager<TUser, TRole>(
        this IServiceCollection services,
        string connectionString,
        Action<AuthManagerOptions>? configure = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
    {
        var options = new AuthManagerOptions();
        configure?.Invoke(options);

        var detected = options.DbProvider.HasValue
            ? MapExplicit(options.DbProvider.Value)
            : DbProviderDetector.Detect(connectionString);

        if (detected == DbProviderDetector.DetectedProvider.Unknown)
            throw new InvalidOperationException(
                "DotNetAuthManager: could not detect the database provider. " +
                "Set options.DbProvider explicitly.");

        services.AddDbContext<AuthManagerAutoDbContext<TUser, TRole>>(
            db => DbProviderDetector.Configure(db, detected, connectionString));

        services.AddIdentity<TUser, TRole>(identity ?? (_ => { }))
                .AddEntityFrameworkStores<AuthManagerAutoDbContext<TUser, TRole>>()
                .AddDefaultTokenProviders();

        services.AddAuthManagerCore<TUser, TRole>(configure);
        return services;
    }

    // =====================================================================
    //  TERTIARY: bring-your-own Identity (already set up)
    // =====================================================================

    /// <summary>
    /// Adds AuthManager services on top of an existing ASP.NET Identity setup.
    /// Use this when you have already called AddIdentity() and AddEntityFrameworkStores().
    /// </summary>
    public static IServiceCollection AddAuthManager<TUser>(
        this IServiceCollection services,
        Action<AuthManagerOptions>? configure = null)
        where TUser : IdentityUser, new()
        => services.AddAuthManagerCore<TUser, IdentityRole>(configure);

    public static IServiceCollection AddAuthManager<TUser, TRole>(
        this IServiceCollection services,
        Action<AuthManagerOptions>? configure = null)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
        => services.AddAuthManagerCore<TUser, TRole>(configure);

    // =====================================================================
    //  INTERNAL: shared registration
    // =====================================================================

    internal static IServiceCollection AddAuthManagerCore<TUser, TRole>(
        this IServiceCollection services,
        Action<AuthManagerOptions>? configure)
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

        // Scoped (per Blazor circuit)
        services.TryAddScoped<IUserManagementService, UserManagementService<TUser>>();
        services.TryAddScoped<IRoleManagementService, RoleManagementService<TRole>>();
        services.TryAddScoped<IOAuthProviderService, OAuthProviderService>();
        services.TryAddScoped<IJwtConfigService, JwtConfigService>();

        // Blazor (idempotent with AddRazorComponents if called by host)
        services.AddRazorComponents()
                .AddInteractiveServerComponents();

        services.AddHttpContextAccessor();

        // Register the SuperAdmin seeder (only runs if options.SeedSuperAdmin = true)
        services.AddHostedService<SuperAdminSeeder<TUser, TRole>>();

        return services;
    }

    private static DbProviderDetector.DetectedProvider MapExplicit(AuthManagerDbProvider p) => p switch
    {
        AuthManagerDbProvider.SqlServer  => DbProviderDetector.DetectedProvider.SqlServer,
        AuthManagerDbProvider.PostgreSQL => DbProviderDetector.DetectedProvider.PostgreSQL,
        AuthManagerDbProvider.MySql      => DbProviderDetector.DetectedProvider.MySql,
        AuthManagerDbProvider.Sqlite     => DbProviderDetector.DetectedProvider.Sqlite,
        _ => DbProviderDetector.DetectedProvider.Unknown
    };

    // Thin IHostedService just to write a startup log line — not a real hosted task
    private sealed class ProviderLogStartup(string providerName, string csName, ILoggerFactory loggerFactory)
        : IHostedService
    {
        public Task StartAsync(CancellationToken ct)
        {
            var logger = loggerFactory.CreateLogger("DotNetAuthManager");
            logger.LogInformation(
                "DotNetAuthManager: using {Provider} provider (connection string: '{CsName}')",
                providerName, csName);
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
