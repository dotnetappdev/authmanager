using AuthManager.AspNetCore.Extensions;
using AuthManager.Core.Options;
using AuthManager.Storage.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using SampleApp.Mvc.Data;

// --------------------------------------------------------
// Bootstrap Serilog (logs flow to AuthManager log viewer)
// --------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    // The AuthManager sink is added after the app is built (see below)
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// --------------------------------------------------------
// MVC + Views
// --------------------------------------------------------
builder.Services.AddControllersWithViews();

// --------------------------------------------------------
// AuthManager — SQL Server + ASP.NET Identity
// --------------------------------------------------------
builder.Services.AddAuthManagerWithSqlServer<ApplicationUser>(
    connectionString: builder.Configuration.GetConnectionString("Default")
                      ?? "Data Source=authmanager-sample.db",          // SQLite fallback for demo
    authManager: options =>
    {
        options.RoutePrefix = "authmanager";
        options.Title = "My App — Auth Manager";
        options.DefaultTheme = AuthManagerTheme.Dark;
        options.AdminRoles = ["Admin"];
        options.Jwt = new JwtOptions
        {
            Issuer = "https://localhost:5001",
            Audience = "https://localhost:5001/api",
            AccessTokenExpiryMinutes = 60,
            EnableRefreshTokens = true
        };
        options.OAuth = new OAuthOptions
        {
            Google = new GoogleOAuthOptions
            {
                Enabled = false,
                ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "",
                ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? ""
            },
            Microsoft = new MicrosoftOAuthOptions
            {
                Enabled = false,
                ClientId = builder.Configuration["Authentication:Microsoft:ClientId"] ?? ""
            }
        };
    },
    identity: options =>
    {
        // Password requirements
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        // Lockout
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        // User
        options.User.RequireUniqueEmail = true;
    }
);

var app = builder.Build();

// --------------------------------------------------------
// Middleware pipeline
// --------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// --------------------------------------------------------
// AuthManager — maps /authmanager and sets up Blazor hub
// --------------------------------------------------------
app.MapAuthManager();

// MVC routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Seed admin user on startup
await SeedAdminUser(app);

app.Run();

// --------------------------------------------------------
// Seed helper
// --------------------------------------------------------
static async Task SeedAdminUser(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    // Ensure Admin role exists
    if (!await roleManager.RoleExistsAsync("Admin"))
        await roleManager.CreateAsync(new IdentityRole("Admin"));

    // Seed admin user
    const string adminEmail = "admin@example.com";
    if (await userManager.FindByEmailAsync(adminEmail) is null)
    {
        var admin = new ApplicationUser
        {
            UserName = "admin",
            Email = adminEmail,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(admin, "Admin@123456!");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }
}
