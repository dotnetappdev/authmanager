using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SampleApp.BlazorWebApp.Components;
using SampleApp.BlazorWebApp.Data;
using Serilog;
using Serilog.Events;

// ── Bootstrap logger ─────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ── 1. Blazor + MudBlazor ─────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// ── 2. DbContext ──────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")!));

// ── 3. ASP.NET Identity ───────────────────────────────────────────────────────
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.SignIn.RequireConfirmedAccount = false; // set true in production
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Cookie settings — adjust in production
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath        = "/account/login";
    o.LogoutPath       = "/account/logout";
    o.AccessDeniedPath = "/account/access-denied";
    o.SlidingExpiration = true;
    o.ExpireTimeSpan   = TimeSpan.FromHours(8);
});

builder.Services.AddCascadingAuthenticationState();

// ── 4. AuthManager on top of Identity ────────────────────────────────────────
//       AddRazorComponents() above is idempotent — AuthManager won't re-register it.
builder.Services.AddAuthManager<IdentityUser>(options =>
{
    options.RoutePrefix    = "authmanager";
    options.Title          = "Blazor App — Auth Manager";
    options.DefaultTheme   = AuthManagerTheme.Dark;
    options.SuperAdminRole = "SuperAdmin";
    options.SeedSuperAdmin         = true;
    options.SeedSuperAdminEmail    = "superadmin@example.com";
    options.SeedSuperAdminPassword = "SuperAdmin@123456!";

    options.PasswordPolicy.MinimumLength       = 8;
    options.PasswordPolicy.RequireUppercase     = true;
    options.PasswordPolicy.RequireDigit         = true;
    options.PasswordPolicy.PasswordHistoryCount = 3;

    options.SecurityPolicy.MaxFailedLoginAttempts = 5;
    options.SecurityPolicy.LockoutDuration        = TimeSpan.FromMinutes(15);
    options.SecurityPolicy.InvalidateSessionsOnPasswordChange = true;
});

var app = builder.Build();

// ── DB migration ──────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ── Middleware ────────────────────────────────────────────────────────────────
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── AuthManager ───────────────────────────────────────────────────────────────
app.MapAuthManager();   // → /authmanager (SuperAdmin only)

// ── Blazor app ────────────────────────────────────────────────────────────────
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

Log.Information("Blazor app running. Visit / for the app, /authmanager for identity management.");
app.Run();
