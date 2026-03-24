using AuthManager.Core.Options;
using AuthManager.AspNetCore.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuthManager.Storage.SqlServer;

/// <summary>
/// Extension methods for using SQL Server with DotNetAuthManager.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AuthManager with SQL Server storage.
    /// Sets up ASP.NET Identity, EF Core, and all AuthManager services in one call.
    /// </summary>
    /// <example>
    /// builder.Services.AddAuthManagerWithSqlServer&lt;ApplicationUser&gt;(
    ///     connectionString: builder.Configuration.GetConnectionString("Default")!,
    ///     authManager: options => options.RoutePrefix = "authmanager"
    /// );
    /// </example>
    public static IServiceCollection AddAuthManagerWithSqlServer<TUser>(
        this IServiceCollection services,
        string connectionString,
        Action<AuthManagerOptions>? authManager = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        => services.AddAuthManagerWithSqlServer<TUser, IdentityRole>(
            connectionString, authManager, identity);

    public static IServiceCollection AddAuthManagerWithSqlServer<TUser, TRole>(
        this IServiceCollection services,
        string connectionString,
        Action<AuthManagerOptions>? authManager = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
    {
        // Register a default DbContext if none already registered
        services.AddDbContext<AuthManagerSqlServerDbContext<TUser, TRole>>(options =>
            options.UseSqlServer(connectionString,
                sql => sql.MigrationsAssembly(typeof(AuthManagerSqlServerDbContext).Assembly.FullName)));

        // Register Identity against that context
        services.AddIdentity<TUser, TRole>(identity ?? (_ => { }))
                .AddEntityFrameworkStores<AuthManagerSqlServerDbContext<TUser, TRole>>()
                .AddDefaultTokenProviders();

        // Register AuthManager services
        services.AddAuthManager<TUser, TRole>(authManager);

        return services;
    }
}
