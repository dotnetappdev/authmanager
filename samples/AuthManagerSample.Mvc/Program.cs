using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

builder.Services.AddControllersWithViews();

// ── 1. Your own DbContext (any provider you like) ────────────────────────
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")!));

// ── 2. Your own Identity setup ───────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequireDigit             = true;
    o.Password.RequiredLength           = 8;
    o.Password.RequireUppercase         = true;
    o.Lockout.MaxFailedAccessAttempts   = 5;
    o.Lockout.DefaultLockoutTimeSpan    = TimeSpan.FromMinutes(15);
    o.User.RequireUniqueEmail           = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ── 3. AuthManager — just lays on top, no DB config needed ───────────────
builder.Services.AddAuthManager<ApplicationUser>(options =>
{
    options.RoutePrefix    = "authmanager";
    options.Title          = "Sample MVC — Auth Manager";
    options.DefaultTheme   = AuthManagerTheme.Dark;
    options.SuperAdminRole = "SuperAdmin";

    // Seed a default SuperAdmin on first run.
    // ⚠️  Set SeedSuperAdmin = false after first login + password change.
    options.SeedSuperAdmin         = true;
    options.SeedSuperAdminEmail    = "superadmin@example.com";
    options.SeedSuperAdminPassword = "SuperAdmin@123456!";

    options.Jwt = new JwtOptions
    {
        Issuer                   = "https://localhost:5001",
        Audience                 = "https://localhost:5001/api",
        AccessTokenExpiryMinutes = 60,
        EnableRefreshTokens      = true
    };
});

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

// ── 4. Maps /authmanager — SuperAdmin role required ──────────────────────
app.MapAuthManager();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
