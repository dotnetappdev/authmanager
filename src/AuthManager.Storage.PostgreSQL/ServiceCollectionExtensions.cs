using AuthManager.Core.Options;
using AuthManager.AspNetCore.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuthManager.Storage.PostgreSQL;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AuthManager with PostgreSQL (Npgsql) storage.
    /// </summary>
    /// <example>
    /// builder.Services.AddAuthManagerWithPostgreSQL&lt;ApplicationUser&gt;(
    ///     connectionString: builder.Configuration.GetConnectionString("Default")!
    /// );
    /// </example>
    public static IServiceCollection AddAuthManagerWithPostgreSQL<TUser>(
        this IServiceCollection services,
        string connectionString,
        Action<AuthManagerOptions>? authManager = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        => services.AddAuthManagerWithPostgreSQL<TUser, IdentityRole>(connectionString, authManager, identity);

    public static IServiceCollection AddAuthManagerWithPostgreSQL<TUser, TRole>(
        this IServiceCollection services,
        string connectionString,
        Action<AuthManagerOptions>? authManager = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
    {
        services.AddDbContext<AuthManagerPostgreSQLDbContext<TUser, TRole>>(options =>
            options.UseNpgsql(connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(AuthManagerPostgreSQLDbContext).Assembly.FullName)));

        services.AddIdentity<TUser, TRole>(identity ?? (_ => { }))
                .AddEntityFrameworkStores<AuthManagerPostgreSQLDbContext<TUser, TRole>>()
                .AddDefaultTokenProviders();

        services.AddAuthManager<TUser, TRole>(authManager);
        return services;
    }
}
