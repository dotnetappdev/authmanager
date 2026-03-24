using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.Storage.PostgreSQL;

public class AuthManagerPostgreSQLDbContext : AuthManagerPostgreSQLDbContext<IdentityUser, IdentityRole>
{
    public AuthManagerPostgreSQLDbContext(DbContextOptions options) : base(options) { }
}

public class AuthManagerPostgreSQLDbContext<TUser, TRole> : IdentityDbContext<TUser, TRole, string>
    where TUser : IdentityUser
    where TRole : IdentityRole
{
    public AuthManagerPostgreSQLDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("auth");
    }
}
