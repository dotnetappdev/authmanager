using AuthManagerWebApi1.Data;
using AuthManagerWebApi1.Identity;
using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ── 1. Your own DbContext ────────────────────────────────────────────────────
// SQLite by default (zero install) — set Database:Provider to "SqlServer" and
// ConnectionStrings:Default to a SQL Server connection string to switch.
var databaseProvider = builder.Configuration["Database:Provider"] ?? "SQLite";
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        o.UseSqlServer(builder.Configuration.GetConnectionString("Default")!);
    else
        o.UseSqlite(builder.Configuration.GetConnectionString("Default")!);
});

// ── 2. Your own Identity ─────────────────────────────────────────────────────
// Stores.SchemaVersion = Version3 adds passkey (WebAuthn) support to the EF store —
// see https://aka.ms/aspnet/passkeys — required for AuthManager's /authmanager/api/passkeys/*
// endpoints and the Passkeys admin page.
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ── 3. AuthManager on top ─────────────────────────────────────────────────────
// A single call wires up the admin UI (Blazor, at /authmanager) *and* the REST API
// (/authmanager/api/*) — the same "batteries included" idea as .NET Aspire's templates.
builder.Services.AddAuthManager<ApplicationUser>(options =>
{
    options.RoutePrefix    = "authmanager";
    options.Title          = "AuthManagerWebApi1";
    options.DefaultTheme   = AuthManagerTheme.System;
    options.SuperAdminRole = "SuperAdmin";

    options.SeedSuperAdmin         = true;
    options.SeedSuperAdminEmail    = "superadmin@example.com";
    options.SeedSuperAdminPassword = "SuperAdmin@123456!"; // change this before deploying

    options.InternalDatabaseProvider = databaseProvider;
    options.InternalDatabaseConnectionString = databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
        ? builder.Configuration.GetConnectionString("Default")!
        : "Data Source=authmanager-internal.db";
});

var app = builder.Build();

// ── DB init ────────────────────────────────────────────────────────────────────
// EnsureCreated builds the Identity tables (AspNetUsers, AspNetRoles, …) on first run.
// Must run before app.Run() so the SuperAdmin seeder can find them at startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── AuthManager — Blazor admin UI + REST API, both under /authmanager ────────
app.MapAuthManager();

// ── Your own app's endpoints go here ──────────────────────────────────────────
app.MapPost("/register", async (RegisterRequest req, UserManager<ApplicationUser> users) =>
{
    var user = new ApplicationUser { UserName = req.Email, Email = req.Email };
    var result = await users.CreateAsync(user, req.Password);
    return result.Succeeded
        ? Results.Ok(new { user.Id, user.Email })
        : Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
}).WithTags("Account");

app.MapPost("/login", async (LoginRequest req, SignInManager<ApplicationUser> signIn) =>
{
    var result = await signIn.PasswordSignInAsync(req.Email, req.Password, isPersistent: true, lockoutOnFailure: true);
    return result.Succeeded ? Results.Ok(new { message = "Signed in." }) : Results.Unauthorized();
}).WithTags("Account");

app.MapGet("/", () => new { message = "Visit /authmanager (SuperAdmin only) or /swagger." });

app.Run();

internal sealed record RegisterRequest(string Email, string Password);
internal sealed record LoginRequest(string Email, string Password);
