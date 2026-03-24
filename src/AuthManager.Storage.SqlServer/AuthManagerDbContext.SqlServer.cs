using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.Storage.SqlServer;

/// <summary>
/// A ready-to-use DbContext with ASP.NET Identity on SQL Server.
/// Inherit from this to extend with your own entities.
/// </summary>
public class AuthManagerSqlServerDbContext : AuthManagerSqlServerDbContext<IdentityUser, IdentityRole>
{
    public AuthManagerSqlServerDbContext(DbContextOptions options) : base(options) { }
}

public class AuthManagerSqlServerDbContext<TUser, TRole> : IdentityDbContext<TUser, TRole, string>
    where TUser : IdentityUser
    where TRole : IdentityRole
{
    public AuthManagerSqlServerDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("auth");
    }
}
