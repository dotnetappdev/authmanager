using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SampleApp.BlazorWebApp.Data;

/// <summary>
/// The host app's DbContext — AuthManager uses this via UserManager/RoleManager.
/// </summary>
public sealed class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
