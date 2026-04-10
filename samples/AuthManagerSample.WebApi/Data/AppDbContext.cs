using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthManagerSample.WebApi.Data;

/// <summary>
/// The host app's DbContext — AuthManager uses this directly via UserManager/RoleManager.
/// AuthManager never owns a database; it layers on top of whatever you bring.
/// </summary>
public sealed class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
