using AuthManager.Core.Options;
using AuthManager.Core.Services;
using AuthManager.AspNetCore.Infrastructure;
using AuthManager.AspNetCore.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AuthManager.AspNetCore.Extensions;

/// <summary>
/// Extension methods for configuring DotNetAuthManager in the DI container.
///
/// Works like .NET Aspire — one or two calls, nothing else needed.
///
/// Primary API (recommended):
///   builder.Services.AddAuthManager&lt;ApplicationUser&gt;(builder.Configuration, options => { ... });
///   — scans ALL connection strings in appsettings.json, picks the first recognisable one.
///   — set options.DbProvider = AuthManagerDbProvider.SqlServer in one line to override detection.
/// </summary>
public static class ServiceCollectionExtensions
{
    // =====================================================================
    //  PRIMARY: IConfiguration — scans all connection strings automatically
    // =====================================================================

    /// <summary>
    /// Adds AuthManager. Scans all connection strings in appsettings.json and auto-detects
    /// the database provider (SQL Server, PostgreSQL, MySQL, SQLite).
    ///
    /// To override auto-detection, set <c>options.DbProvider</c> in one line:
    /// <code>options.DbProvider = AuthManagerDbProvider.SqlServer;</code>
    /// </summary>
    /// <example>
    /// // appsettings.json — any of these work automatically, no extra code needed:
    /// // "ConnectionStrings": { "Default": "Server=.;Database=myapp;Trusted_Connection=True;" }
    /// // "ConnectionStrings": { "Db":      "Host=localhost;Database=myapp;Username=app;" }
    /// // "ConnectionStrings": { "App":     "Data Source=myapp.db" }
    ///
    /// builder.Services.AddAuthManager&lt;ApplicationUser&gt;(builder.Configuration, options =>
    /// {
    ///     options.RoutePrefix    = "authmanager";
    ///     options.DefaultTheme   = AuthManagerTheme.Dark;
    ///     options.SeedSuperAdmin = true;
    ///
    ///     // One-line provider override (optional — auto-detected if omitted):
    ///     // options.DbProvider = AuthManagerDbProvider.SqlServer;
    /// });
    /// </example>
    public static IServiceCollection AddAuthManager<TUser>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuthManagerOptions>? configure = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        => services.AddAuthManager<TUser, IdentityRole>(configuration, configure, identity);

    /// <inheritdoc cref="AddAuthManager{TUser}(IServiceCollection,IConfiguration,Action{AuthManagerOptions}?,Action{IdentityOptions}?)"/>
    public static IServiceCollection AddAuthManager<TUser, TRole>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AuthManagerOptions>? configure = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
    {
        // Read options early so we can inspect DbProvider / ConnectionStringName
        var opts = new AuthManagerOptions();
        configure?.Invoke(opts);

        var wantedProvider = opts.DbProvider.HasValue
            ? MapExplicit(opts.DbProvider.Value)
            : DbProviderDetector.DetectedProvider.Unknown;

        // Scan ALL connection strings — picks the right one automatically.
        // ConnectionStringName acts as a fallback hint only when scanning finds nothing.
        var (connectionString, detected) = DbProviderDetector.Resolve(
            configuration, wantedProvider, fallbackKey: opts.ConnectionStringName);

        var (providerName, _) = DbProviderDetector.GetProviderInfo(detected);

        // Unified DbContext — Use{Provider} is called via reflection, no hard package dependency
        services.AddDbContext<AuthManagerAutoDbContext<TUser, TRole>>(db =>
            DbProviderDetector.Configure(db, detected, connectionString));

        services.AddIdentity<TUser, TRole>(identity ?? (_ => { }))
                .AddEntityFrameworkStores<AuthManagerAutoDbContext<TUser, TRole>>()
                .AddDefaultTokenProviders();

        services.AddAuthManagerCore<TUser, TRole>(configure);

        // Startup log: "DotNetAuthManager: using SQL Server"
        services.AddSingleton<IHostedService>(sp =>
            new ProviderLogStartup(providerName, sp.GetRequiredService<ILoggerFactory>()));

        return services;
    }

    // =====================================================================
    //  SECONDARY: explicit connection string (runtime-computed strings)
    // =====================================================================

    /// <summary>
    /// Adds AuthManager with an explicit connection string (e.g. built at runtime).
    /// Provider is auto-detected from the string, or set via <c>options.DbProvider</c>.
    /// Prefer the IConfiguration overload for most apps.
    /// </summary>
    public static IServiceCollection AddAuthManager<TUser>(
        this IServiceCollection services,
        string connectionString,
        Action<AuthManagerOptions>? configure = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        => services.AddAuthManager<TUser, IdentityRole>(connectionString, configure, identity);

    /// <inheritdoc cref="AddAuthManager{TUser}(IServiceCollection,string,Action{AuthManagerOptions}?,Action{IdentityOptions}?)"/>
    public static IServiceCollection AddAuthManager<TUser, TRole>(
        this IServiceCollection services,
        string connectionString,
        Action<AuthManagerOptions>? configure = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
    {
        var opts = new AuthManagerOptions();
        configure?.Invoke(opts);

        var detected = opts.DbProvider.HasValue
            ? MapExplicit(opts.DbProvider.Value)
            : DbProviderDetector.Detect(connectionString);

        if (detected == DbProviderDetector.DetectedProvider.Unknown)
            throw new InvalidOperationException(
                "DotNetAuthManager: could not detect provider from the supplied connection string. " +
                "Set options.DbProvider = AuthManagerDbProvider.SqlServer (or PostgreSQL/MySql/Sqlite).");

        services.AddDbContext<AuthManagerAutoDbContext<TUser, TRole>>(
            db => DbProviderDetector.Configure(db, detected, connectionString));

        services.AddIdentity<TUser, TRole>(identity ?? (_ => { }))
                .AddEntityFrameworkStores<AuthManagerAutoDbContext<TUser, TRole>>()
                .AddDefaultTokenProviders();

        services.AddAuthManagerCore<TUser, TRole>(configure);
        return services;
    }

    // =====================================================================
    //  TERTIARY: bring-your-own Identity (already set up in the host app)
    // =====================================================================

    /// <summary>
    /// Adds AuthManager services on top of an existing ASP.NET Identity setup.
    /// Use this when you have already called AddIdentity() and AddEntityFrameworkStores().
    /// No connection string needed — Identity is already wired.
    /// </summary>
    public static IServiceCollection AddAuthManager<TUser>(
        this IServiceCollection services,
        Action<AuthManagerOptions>? configure = null)
        where TUser : IdentityUser, new()
        => services.AddAuthManagerCore<TUser, IdentityRole>(configure);

    /// <inheritdoc cref="AddAuthManager{TUser}(IServiceCollection,Action{AuthManagerOptions}?)"/>
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

        services.TryAddSingleton<LogAggregationService>();
        services.TryAddSingleton<ILogAggregationService>(sp => sp.GetRequiredService<LogAggregationService>());
        services.TryAddSingleton<IAuditService, InMemoryAuditService>();

        services.TryAddScoped<IUserManagementService, UserManagementService<TUser>>();
        services.TryAddScoped<IRoleManagementService, RoleManagementService<TRole>>();
        services.TryAddScoped<IOAuthProviderService, OAuthProviderService>();
        services.TryAddScoped<IJwtConfigService, JwtConfigService>();

        services.AddRazorComponents()
                .AddInteractiveServerComponents();

        services.AddHttpContextAccessor();

        services.AddHostedService<SuperAdminSeeder<TUser, TRole>>();

        return services;
    }

    private static DbProviderDetector.DetectedProvider MapExplicit(AuthManagerDbProvider p) => p switch
    {
        AuthManagerDbProvider.SqlServer  => DbProviderDetector.DetectedProvider.SqlServer,
        AuthManagerDbProvider.PostgreSQL => DbProviderDetector.DetectedProvider.PostgreSQL,
        AuthManagerDbProvider.MySql      => DbProviderDetector.DetectedProvider.MySql,
        AuthManagerDbProvider.Sqlite     => DbProviderDetector.DetectedProvider.Sqlite,
        _                                => DbProviderDetector.DetectedProvider.Unknown
    };

    private sealed class ProviderLogStartup(string providerName, ILoggerFactory loggerFactory)
        : IHostedService
    {
        public Task StartAsync(CancellationToken ct)
        {
            loggerFactory.CreateLogger("DotNetAuthManager")
                .LogInformation("DotNetAuthManager: using {Provider} provider", providerName);
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
