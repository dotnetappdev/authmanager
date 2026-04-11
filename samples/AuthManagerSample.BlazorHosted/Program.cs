using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using AuthManagerSample.BlazorHosted;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ── 1. Blazor setup ───────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// ── 2. Your own DbContext (SQLite — zero install) ─────────────────────────
var connection = builder.Configuration.GetConnectionString("Default") ?? "Data Source=blazorhosted.db";
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));

// ── 3. Your own Identity ──────────────────────────────────────────────────
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath        = "/account/login";
    o.LogoutPath       = "/account/logout";
    o.SlidingExpiration = true;
    o.ExpireTimeSpan   = TimeSpan.FromHours(8);
});

builder.Services.AddCascadingAuthenticationState();

// ── 4. AuthManager on top ─────────────────────────────────────────────────
builder.Services.AddAuthManager<IdentityUser>(options =>
{
    options.RoutePrefix    = "authmanager";
    options.Title          = "Blazor Hosted — Auth Manager";
    options.DefaultTheme   = AuthManagerTheme.Dark;
    options.SuperAdminRole = "SuperAdmin";

    // Seed the default SuperAdmin on first run.
    // ⚠️  Set SeedSuperAdmin = false after first login + password change.
    options.SeedSuperAdmin         = true;
    options.SeedSuperAdminEmail    = "superadmin@example.com";
    options.SeedSuperAdminPassword = "SuperAdmin@123456!";
});

var app = builder.Build();

// ── DB init ───────────────────────────────────────────────────────────────
// EnsureCreated creates the Identity tables (AspNetUsers, AspNetRoles, …)
// on the first run. Must run BEFORE app.Run() so that the SuperAdminSeeder
// hosted service can find the tables when it starts.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapAuthManager();   // → /authmanager (SuperAdmin only)

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

Log.Information("Blazor hosted app running. Visit / for the app, /authmanager for identity management.");
app.Run();

// ── App DbContext ─────────────────────────────────────────────────────────
public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
