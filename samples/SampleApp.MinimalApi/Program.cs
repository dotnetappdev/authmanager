// ---- DotNetAuthManager — Minimal API Sample ----
// Reads connection string from appsettings.json.
// Auto-detects provider (SQLite in this sample).
using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ---- AuthManager — auto-detects SQLite from appsettings.json ----
builder.Services.AddAuthManager<IdentityUser>(
    builder.Configuration,
    options =>
    {
        options.RoutePrefix    = "authmanager";
        options.Title          = "Minimal API Auth Manager";
        options.DefaultTheme   = AuthManagerTheme.Dark;
        options.SuperAdminRole = "SuperAdmin";
        options.SeedSuperAdmin = true;
        options.SeedSuperAdminEmail    = "superadmin@example.com";
        options.SeedSuperAdminPassword = "SuperAdmin@123456!";
    }
);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthManager();

app.MapGet("/", () => new { Message = "Visit /authmanager (SuperAdmin only)" })
   .WithTags("Info");

app.Run();

using Microsoft.AspNetCore.Identity;
