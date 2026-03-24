using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Infrastructure;

/// <summary>
/// A generic Identity DbContext used when AddAuthManager(IConfiguration) auto-detects
/// the database provider. The provider-specific Use{X}() call is made at registration
/// time via <see cref="DbProviderDetector"/>.
///
/// If you need to customise the schema, inherit from this class in your own project.
/// </summary>
public class AuthManagerAutoDbContext
    : AuthManagerAutoDbContext<IdentityUser, IdentityRole>
{
    public AuthManagerAutoDbContext(DbContextOptions options) : base(options) { }
}

public class AuthManagerAutoDbContext<TUser, TRole>
    : IdentityDbContext<TUser, TRole, string>
    where TUser : IdentityUser
    where TRole : IdentityRole
{
    public AuthManagerAutoDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Customise here or in a derived class
    }
}
