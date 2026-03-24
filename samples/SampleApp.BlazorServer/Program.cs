// ---- DotNetAuthManager — Blazor Server Sample ----
using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
using MudBlazor.Services;
using Serilog;
using Serilog.Events;
using SampleApp.BlazorServer.Components;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ---- Host app's own Blazor setup ----
builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// ---- AuthManager — auto-detects SQLite from appsettings.json ----
// AddAuthManager() is idempotent with AddRazorComponents() above.
builder.Services.AddAuthManager<IdentityUser>(
    builder.Configuration,
    options =>
    {
        options.RoutePrefix    = "authmanager";
        options.Title          = "Blazor App — Auth Manager";
        options.DefaultTheme   = AuthManagerTheme.Dark;
        options.SuperAdminRole = "SuperAdmin";
        options.SeedSuperAdmin = true;
        options.SeedSuperAdminEmail    = "superadmin@example.com";
        options.SeedSuperAdminPassword = "SuperAdmin@123456!";
    }
);

var app = builder.Build();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// AuthManager maps first
app.MapAuthManager();

// Host Blazor app
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();

using Microsoft.AspNetCore.Identity;
