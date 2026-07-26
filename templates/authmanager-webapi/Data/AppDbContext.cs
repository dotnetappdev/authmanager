using AuthManagerWebApi1.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthManagerWebApi1.Data;

/// <summary>Your app's own Identity store — AuthManager reads/writes through this via UserManager/RoleManager.</summary>
public sealed class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
