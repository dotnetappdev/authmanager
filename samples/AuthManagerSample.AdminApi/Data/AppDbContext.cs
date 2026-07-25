using AuthManagerSample.AdminApi.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthManagerSample.AdminApi.Data;

/// <summary>Host app's own Identity store — AuthManager reads/writes through this via UserManager/RoleManager.</summary>
public sealed class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
