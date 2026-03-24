// ============================================================
//  DotNetAuthManager — Blazor Server Sample
//  The auth manager coexists with your own Blazor app
// ============================================================
using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
using AuthManager.Storage.SqlServer;
using Microsoft.AspNetCore.Identity;
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

// ---- Your own Blazor app ----
builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

// MudBlazor for your own app (AuthManager also uses MudBlazor)
builder.Services.AddMudServices();

// ---- AuthManager ----
// Note: AddAuthManager() is idempotent with AddRazorComponents() above
builder.Services.AddAuthManagerWithSqlServer<IdentityUser>(
    connectionString: "Data Source=authmanager-blazor.db",
    authManager: options =>
    {
        options.RoutePrefix = "authmanager";
        options.Title = "Blazor App — Auth Manager";
        options.DefaultTheme = AuthManagerTheme.Dark;
    }
);

var app = builder.Build();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ---- AuthManager first ----
app.MapAuthManager();

// ---- Your Blazor app ----
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Run();
