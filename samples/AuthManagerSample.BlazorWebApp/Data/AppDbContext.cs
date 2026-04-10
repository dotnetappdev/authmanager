using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AuthManagerSample.BlazorWebApp.Identity;

namespace AuthManagerSample.BlazorWebApp.Data;

/// <summary>
/// The host app's DbContext. AuthManager layers on top via UserManager/RoleManager.
/// </summary>
public sealed class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.FirstName).HasMaxLength(100);
            e.Property(u => u.LastName).HasMaxLength(100);
            e.Property(u => u.ProfilePictureUrl).HasMaxLength(500);
        });
    }
}

