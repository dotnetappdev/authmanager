using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Serilog;
using Serilog.Events;
using AuthManagerSample.BlazorServer.Components;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ── 1. Host app's own Blazor setup ───────────────────────────────────────
builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// ── 2. Your own DbContext ────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")!));

// ── 3. Your own Identity ─────────────────────────────────────────────────
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ── 4. AuthManager on top — no DbContext config needed ───────────────────
// AddRazorComponents() above is idempotent — AuthManager won't double-register it.
builder.Services.AddAuthManager<IdentityUser>(options =>
{
    options.RoutePrefix    = "authmanager";
    options.Title          = "Blazor App — Auth Manager";
    options.DefaultTheme   = AuthManagerTheme.Dark;
    options.SuperAdminRole = "SuperAdmin";
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

app.MapAuthManager();

app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();

// ── Shared app DbContext ─────────────────────────────────────────────────
public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
