using AuthManager.Core.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using AuthManager.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Use SQLite for the sample demo
var connection = builder.Configuration.GetConnectionString("Default") ?? "Data Source=authmanager_local.db";
builder.Services.AddDbContext<AuthManagerDbContext>(options =>
    options.UseSqlite(connection));

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add AuthManager services (will use defaults)
builder.Services.AddAuthManager<ApplicationUser>(options =>
{
    options.RoutePrefix = "authmanager";
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Ensure DB created and seed a default SuperAdmin (safe to run multiple times)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthManagerDbContext>();
    db.Database.Migrate();

    // Create default SuperAdmin if missing — use built-in seeder if available
    try
    {
        await app.CreateDefaultSuperUserAsync<ApplicationUser>(
            email: "superadmin@example.com",
            password: "SuperAdmin@123456!");
    }
    catch { /* ignore seeding errors in sample */ }
}

app.Run();
