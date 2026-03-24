using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
using Serilog;
using Serilog.Events;

// ---- Serilog bootstrap (logs appear in AuthManager log viewer) ----
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddControllersWithViews();

// ----------------------------------------------------------------
//  DotNetAuthManager — reads connection string from appsettings.json
//  and auto-detects the provider (SQL Server / PostgreSQL / MySQL / SQLite).
//
//  appsettings.json:
//  {
//    "ConnectionStrings": {
//      "Default": "Server=.;Database=SampleMvc;Trusted_Connection=True;"
//    }
//  }
// ----------------------------------------------------------------
builder.Services.AddAuthManager<ApplicationUser>(
    builder.Configuration,
    options =>
    {
        options.RoutePrefix    = "authmanager";
        options.Title          = "Sample MVC — Auth Manager";
        options.DefaultTheme   = AuthManagerTheme.Dark;

        // Only users in the SuperAdmin role can access the management UI.
        // All other authenticated users are denied.
        options.SuperAdminRole = "SuperAdmin";

        // Seed a default SuperAdmin user and role on first startup.
        // ⚠️  Remove SeedSuperAdmin = true once you have logged in and changed the password.
        options.SeedSuperAdmin         = true;
        options.SeedSuperAdminEmail    = "superadmin@example.com";
        options.SeedSuperAdminPassword = "SuperAdmin@123456!";

        // JWT settings
        options.Jwt = new JwtOptions
        {
            Issuer   = "https://localhost:5001",
            Audience = "https://localhost:5001/api",
            AccessTokenExpiryMinutes = 60,
            EnableRefreshTokens = true
        };
    },
    identity =>
    {
        identity.Password.RequireDigit       = true;
        identity.Password.RequiredLength     = 8;
        identity.Password.RequireUppercase   = true;
        identity.Lockout.MaxFailedAccessAttempts = 5;
        identity.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
        identity.User.RequireUniqueEmail     = true;
    }
);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Maps /authmanager — SuperAdmin role required (no exceptions)
app.MapAuthManager();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// ---- ApplicationUser (extend with your own fields) ----
public class ApplicationUser : Microsoft.AspNetCore.Identity.IdentityUser
{
    public string? FullName { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
