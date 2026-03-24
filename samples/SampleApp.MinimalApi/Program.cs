// ============================================================
//  DotNetAuthManager — Minimal API Sample
//  Shows how to add the auth manager alongside a JSON API
// ============================================================
using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
using AuthManager.Storage.SqlServer;
using Microsoft.AspNetCore.Identity;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}")
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ---- AuthManager with SQL Server ----
builder.Services.AddAuthManagerWithSqlServer<IdentityUser>(
    connectionString: "Data Source=authmanager-minimal.db",
    authManager: options =>
    {
        options.RoutePrefix = "authmanager";
        options.Title = "Minimal API Auth Manager";
        options.DefaultTheme = AuthManagerTheme.Dark;
        options.AdminRoles = ["Admin"];
    }
);

// ---- Swagger (optional) ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// ---- AuthManager UI at /authmanager ----
app.MapAuthManager();

// ---- Your API routes ----
app.MapGet("/", () => new { Message = "Hello! Visit /authmanager to manage users." })
   .WithTags("Info");

app.MapGet("/api/secure", () => new { Data = "This requires auth" })
   .RequireAuthorization()
   .WithTags("API");

app.Run();
