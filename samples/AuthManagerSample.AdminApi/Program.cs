using System.Text;
using AuthManager.AspNetCore.Extensions;
using JwtOptions = AuthManagerSample.AdminApi.Services.JwtOptions;
using AuthManagerSample.AdminApi.Data;
using AuthManagerSample.AdminApi.Identity;
using AuthManagerSample.AdminApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── 1. DbContext — this sample's own Identity store ──────────────────────────
// Standalone by default (SQLite, zero install), or point it at SQL Server for production —
// same "Database:Provider" switch AuthManager itself uses for its internal store
// (InternalDatabaseProvider), so both halves of the app can share one SQL Server if you want.
var databaseProvider = builder.Configuration["Database:Provider"] ?? "SQLite";
builder.Services.AddDbContext<AppDbContext>(o =>
{
    if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        o.UseSqlServer(builder.Configuration.GetConnectionString("Default")!);
    else
        o.UseSqlite(builder.Configuration.GetConnectionString("Default")!);
});

// ── 2. ASP.NET Identity ───────────────────────────────────────────────────────
// Stores.SchemaVersion = Version3 is what adds passkey (WebAuthn) support to the EF store —
// see https://aka.ms/aspnet/passkeys. Required for AuthManager's /authmanager/api/passkeys/*
// endpoints to work; without it they 500 with "IdentityUserPasskey not in model".
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(o =>
    {
        o.User.RequireUniqueEmail = true;
        o.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ── 3. JWT bearer authentication ──────────────────────────────────────────────
// AuthManager doesn't care how you authenticate callers — cookies, JWT, an external
// OIDC provider all work. It only ever checks ClaimsPrincipal.IsInRole(SuperAdminRole).
var jwtSection = builder.Configuration.GetSection(JwtOptions.Section);
builder.Services.Configure<JwtOptions>(jwtSection);
var jwtOpts = jwtSection.Get<JwtOptions>()!;

builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOpts.Issuer,
            ValidAudience = jwtOpts.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpts.SecretKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();

// ── 4. AuthManager — headless mode ────────────────────────────────────────────
// Same AddAuthManager<TUser>() call as the Blazor samples. What differs is what we map
// below: MapAuthManagerApi() instead of MapAuthManager(), so no Razor Components/MudBlazor
// endpoints are ever registered — this process serves pure JSON, nothing else.
builder.Services.AddAuthManager<ApplicationUser>(options =>
{
    options.RoutePrefix = "authmanager";
    options.SuperAdminRole = "SuperAdmin";
    options.SeedSuperAdmin = true;
    options.SeedSuperAdminEmail = "superadmin@example.com";
    options.SeedSuperAdminPassword = "SuperAdmin@123456!";

    options.PasswordPolicy.MinimumLength = 8;
    options.PasswordPolicy.RequireUppercase = true;
    options.PasswordPolicy.RequireDigit = true;

    options.SecurityPolicy.MaxFailedLoginAttempts = 5;
    options.SecurityPolicy.LockoutDuration = TimeSpan.FromMinutes(15);

    // AuthManager's own internal store (audit log, sessions, tokens, licenses, etc.) follows
    // the same Database:Provider switch as the Identity store above, so one config setting
    // moves the whole app between "standalone SQLite" and "SQL Server" — set Database:Provider
    // to "SqlServer" and ConnectionStrings:Default to a SQL Server connection string.
    options.InternalDatabaseProvider = databaseProvider;
    options.InternalDatabaseConnectionString = databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
        ? builder.Configuration.GetConnectionString("Default")!
        : "Data Source=authmanager-internal.db";

    // Same key/issuer/audience as the JWT bearer scheme above, so tokens AuthManager issues
    // for OAuth2 clients (client_credentials grant) validate against this app's own APIs too.
    options.Jwt.SigningKey = jwtOpts.SecretKey;
    options.Jwt.Issuer = jwtOpts.Issuer;
    options.Jwt.Audience = jwtOpts.Audience;
    options.Jwt.AccessTokenExpiryMinutes = jwtOpts.AccessTokenExpiryMinutes;
});

// ── 5. Swagger — browse the full admin REST API surface ──────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuthManager Admin API",
        Version = "v1",
        Description = "Headless AuthManager — the full admin surface (users, roles, groups, " +
                       "tenants, sessions, tokens, audit) as JSON, no Blazor UI. " +
                       "POST /login to get a token, then Authorize with it below."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT access token from POST /login"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ── DB init ────────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthManager Admin API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();

// ── AuthManager — REST API only, no Blazor components mapped anywhere ────────
app.MapAuthManagerApi();

// ── This sample's own login endpoint, issuing the JWT the admin API expects ──
app.MapPost("/login", async (LoginRequest req, TokenService tokens) =>
{
    var token = await tokens.IssueAccessTokenAsync(req.Email, req.Password);
    return token is null ? Results.Unauthorized() : Results.Ok(new { accessToken = token });
}).WithTags("Auth").WithName("Login");

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

internal sealed record LoginRequest(string Email, string Password);

/// <summary>Exposed so integration tests can drive this app via <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
