using System.Text;
using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SampleApp.WebApi.Data;
using SampleApp.WebApi.Endpoints;
using SampleApp.WebApi.Services;
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

// ── 1. DbContext ──────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")!));

// ── 2. ASP.NET Identity ───────────────────────────────────────────────────────
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(o =>
    {
        o.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ── 3. JWT authentication ─────────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection(JwtOptions.Section);
builder.Services.Configure<JwtOptions>(jwtSection);

var jwtOpts = jwtSection.Get<JwtOptions>()!;
builder.Services
    .AddAuthentication(o =>
    {
        o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        o.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtOpts.Issuer,
            ValidAudience            = jwtOpts.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtOpts.SecretKey)),
            ClockSkew                = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

// ── 4. TokenService ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<TokenService>();

// ── 5. AuthManager on top of Identity ────────────────────────────────────────
//       No DB configuration — it uses the AppDbContext + Identity you set up above.
builder.Services.AddAuthManager<IdentityUser>(options =>
{
    options.RoutePrefix    = "authmanager";
    options.Title          = "Web API — Auth Manager";
    options.DefaultTheme   = AuthManagerTheme.Dark;
    options.SuperAdminRole = "SuperAdmin";
    options.SeedSuperAdmin         = true;
    options.SeedSuperAdminEmail    = "superadmin@example.com";
    options.SeedSuperAdminPassword = "SuperAdmin@123456!";

    // Password policy applied to Identity at startup
    options.PasswordPolicy.MinimumLength        = 8;
    options.PasswordPolicy.RequireUppercase      = true;
    options.PasswordPolicy.RequireDigit          = true;
    options.PasswordPolicy.PasswordHistoryCount  = 5;

    // Brute-force protection
    options.SecurityPolicy.MaxFailedLoginAttempts = 5;
    options.SecurityPolicy.LockoutDuration        = TimeSpan.FromMinutes(15);
});

// ── 6. Swagger / OpenAPI ──────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "SampleApp Web API",
        Version = "v1",
        Description = "ASP.NET Core Web API with JWT auth + AuthManager for identity administration."
    });

    // JWT bearer auth scheme for Swagger UI
    var scheme = new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter the JWT access token from POST /api/auth/login"
    };
    c.AddSecurityDefinition("Bearer", scheme);
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

// ── DB migration on startup ───────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SampleApp API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

// ── AuthManager UI ────────────────────────────────────────────────────────────
app.MapAuthManager();   // → /authmanager (SuperAdmin only)

// ── Domain endpoints ──────────────────────────────────────────────────────────
app.MapGroup("/api/auth")
   .WithTags("Auth")
   .MapAuthEndpoints();

app.MapGroup("/api/products")
   .WithTags("Products")
   .MapProductEndpoints();

app.MapGet("/", () => Results.Redirect("/swagger"))
   .ExcludeFromDescription();

// ── Start ─────────────────────────────────────────────────────────────────────
Log.Information("Web API running. Visit /swagger for the API docs, /authmanager for identity management.");
app.Run();
