using AuthManager.Core.Options;
using AuthManager.AspNetCore.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuthManager.Storage.MySql;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AuthManager with MySQL/MariaDB (Pomelo) storage.
    /// </summary>
    public static IServiceCollection AddAuthManagerWithMySql<TUser>(
        this IServiceCollection services,
        string connectionString,
        Action<AuthManagerOptions>? authManager = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        => services.AddAuthManagerWithMySql<TUser, IdentityRole>(connectionString, authManager, identity);

    public static IServiceCollection AddAuthManagerWithMySql<TUser, TRole>(
        this IServiceCollection services,
        string connectionString,
        Action<AuthManagerOptions>? authManager = null,
        Action<IdentityOptions>? identity = null)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
    {
        services.AddDbContext<AuthManagerMySqlDbContext<TUser, TRole>>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString),
                mysql => mysql.MigrationsAssembly(typeof(AuthManagerMySqlDbContext).Assembly.FullName)));

        services.AddIdentity<TUser, TRole>(identity ?? (_ => { }))
                .AddEntityFrameworkStores<AuthManagerMySqlDbContext<TUser, TRole>>()
                .AddDefaultTokenProviders();

        services.AddAuthManager<TUser, TRole>(authManager);
        return services;
    }
}

public class AuthManagerMySqlDbContext : AuthManagerMySqlDbContext<IdentityUser, IdentityRole>
{
    public AuthManagerMySqlDbContext(DbContextOptions options) : base(options) { }
}

public class AuthManagerMySqlDbContext<TUser, TRole> : Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<TUser, TRole, string>
    where TUser : IdentityUser
    where TRole : IdentityRole
{
    public AuthManagerMySqlDbContext(DbContextOptions options) : base(options) { }
}
