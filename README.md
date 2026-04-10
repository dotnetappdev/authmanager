# DotNetAuthManager

[![NuGet](https://img.shields.io/nuget/v/DotNetAuthManager.svg)](https://www.nuget.org/packages/DotNetAuthManager)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A **drop-in ASP.NET Identity management UI** for .NET — inspired by how **.NET Aspire** embeds its dashboard. Drop in a NuGet package, call two methods, and navigate to `/authmanager`.

![Dashboard screenshot](docs/site/assets/img/screenshot-dashboard.png)

---

## Features

| Area | Capability |
|------|-----------|
| **Users** | Full CRUD via MudBlazor DataGrid · Lock/unlock · Password reset · 2FA toggle · Role assignment · Claims editor |
| **Roles** | Create / edit / delete · Assign claims to roles |
| **Claims** | User and role claims management with type reference |
| **Required Actions** | Per-user actions enforced on next sign-in: UpdatePassword, VerifyEmail, ConfigureTOTP, UpdateProfile, AcceptTerms |
| **Custom Attributes** | Store arbitrary key/value attributes per user via `custom:*` claims |
| **Security Settings** | Password Policy UI (length, complexity, history, expiry) · Brute Force Detection (max attempts, lockout duration) · Registration Policy |
| **Active Sessions** | View all tracked sessions · Revoke individual, per-user, or all sessions at once |
| **JWT** | Configure issuer, audience, expiry, algorithm · Test token generator |
| **OAuth** | Google, Microsoft, Apple, GitHub, custom OIDC providers |
| **Logs** | Real-time Serilog viewer with filtering, search, live mode |
| **Audit** | Every change recorded — who, what, when, from where |
| **Import / Export** | Bulk CSV and JSON user import/export |
| **Webhooks** | Signed HTTP POST events to external endpoints on auth actions |
| **Themes** | BookIt dark palette + clean light palette · OS preference auto-detect |
| **Source Gen** | Scaffolds ApplicationUser, DbContext & wiring if Identity is missing |

---

## Architecture

```
DotNetAuthManager  ← one package, that's it
├── AuthManager.Core          — Models, DTOs, service interfaces
├── AuthManager.UI            — Blazor Server RCL (MudBlazor)
└── AuthManager.AspNetCore    — DI extensions, services, SuperAdmin seeder

AuthManager does not own your database.
It uses the UserManager<TUser> and RoleManager<TRole> already in your container.
Bring your own DbContext + Identity — any provider, any schema.

Tooling (optional):
└── DotNetAuthManager.SourceGenerator — Roslyn scaffolding if you have no Identity yet
```

---

## Quick Start

### 1. Install

```bash
dotnet add package DotNetAuthManager
```

### 2. Set up your DbContext and Identity as normal

AuthManager does not touch your database. Set it up however you like:

```csharp
// Any provider — SQL Server, PostgreSQL, MySQL, SQLite, whatever you already use
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")!));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequiredLength        = 8;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.User.RequireUniqueEmail        = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();
```

### 3. Add AuthManager on top

```csharp
builder.Services.AddAuthManager<ApplicationUser>(options =>
{
    options.RoutePrefix    = "authmanager";
    options.DefaultTheme   = AuthManagerTheme.Dark;
    options.SuperAdminRole = "SuperAdmin";   // only this role can enter the UI
});
```

### 4. Create the default SuperAdmin and run

**Option A — explicit call (recommended):**

```csharp
var app = builder.Build();

// Creates the SuperAdmin role + user on first run. Idempotent — safe to leave in.
await app.CreateDefaultSuperUserAsync<ApplicationUser>(
    email:    "superadmin@example.com",
    password: "SuperAdmin@123456!"
);

app.UseAuthentication();
app.UseAuthorization();
app.MapAuthManager();   // → /authmanager
app.Run();
```

**Option B — automatic via hosted service:**

```csharp
builder.Services.AddAuthManager<ApplicationUser>(options =>
{
    options.RoutePrefix    = "authmanager";
    options.SuperAdminRole = "SuperAdmin";

    // Seed on startup. ⚠️ Set false after first login + password change.
    options.SeedSuperAdmin         = true;
    options.SeedSuperAdminEmail    = "superadmin@example.com";
    options.SeedSuperAdminPassword = "SuperAdmin@123456!";
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthManager();
app.Run();
```

### 5. Open the dashboard

Navigate to **`https://localhost:5001/authmanager`**, sign in, change the password.

---

## Serilog Integration

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.AuthManager(app.Services)   // feeds the /authmanager/logs viewer
    .CreateLogger();

builder.Host.UseSerilog();
```

---

## Session Tracking

AuthManager ships an `ISessionService` (in-memory by default). Call `TrackSessionAsync` from your login endpoint to make sessions appear in the **Active Sessions** UI:

```csharp
// In your login action / minimal API handler
var session = new SessionInfo
{
    SessionId         = Guid.NewGuid().ToString(),
    UserId            = user.Id,
    UserName          = user.UserName!,
    CreatedAt         = DateTimeOffset.UtcNow,
    LastActiveAt      = DateTimeOffset.UtcNow,
    IpAddress         = HttpContext.Connection.RemoteIpAddress?.ToString(),
    UserAgent         = Request.Headers.UserAgent,
    DeviceDescription = "Chrome on Windows",  // parse yourself or use a UA library
};
await sessionService.TrackSessionAsync(session);
```

For distributed deployments, replace the in-memory store:

```csharp
// Register BEFORE or AFTER AddAuthManager() — TryAddSingleton is used internally
services.AddSingleton<ISessionService, RedisSessionService>();
```

---

## Required Actions

Assign actions users must complete on their **next sign-in**:

```csharp
// Force a user to set up TOTP on next login
await userManagementService.AddRequiredActionAsync(userId, "ConfigureTOTP");

// Or via the UI: Users → Edit User → Required Actions panel
```

Available action strings: `UpdatePassword`, `VerifyEmail`, `ConfigureTOTP`, `UpdateProfile`, `AcceptTerms`.

Actions are stored as `required_action` claims in ASP.NET Identity. Check them in your auth pipeline:

```csharp
var requiredActions = user.Claims
    .Where(c => c.Type == "required_action")
    .Select(c => c.Value)
    .ToList();

if (requiredActions.Contains("UpdatePassword"))
    return RedirectToAction("ForcePasswordChange");
```

---

## Custom User Attributes

Store arbitrary key/value data per user via `custom:*` claims — accessible from the **Users → Edit User → Custom Attributes** panel or programmatically:

```csharp
// Add a custom attribute
await userManager.AddClaimAsync(user, new Claim("custom:department", "Engineering"));

// Read custom attributes
var claims = await userManager.GetClaimsAsync(user);
var attributes = claims
    .Where(c => c.Type.StartsWith("custom:", StringComparison.OrdinalIgnoreCase))
    .ToDictionary(c => c.Type["custom:".Length..], c => c.Value);
```

---

## Password History

AuthManager enforces password history automatically when `PasswordPolicy.PasswordHistoryCount > 0`. Previous password hashes are stored as `password_history` claims and checked on every password reset:

```csharp
options.PasswordPolicy.PasswordHistoryCount = 5;  // reject last 5 passwords
```

---

## Source Generator (no Identity yet?)

Add to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="DotNetAuthManager.SourceGenerator" Version="*" />
</ItemGroup>

<PropertyGroup>
  <AuthManagerScaffoldIdentity>true</AuthManagerScaffoldIdentity>
  <AuthManagerRootNamespace>MyApp</AuthManagerRootNamespace>
  <AuthManagerDbContextName>ApplicationDbContext</AuthManagerDbContextName>
  <AuthManagerUserName>ApplicationUser</AuthManagerUserName>
  <AuthManagerDbProvider>SqlServer</AuthManagerDbProvider>  <!-- SqlServer | PostgreSQL | MySql -->
</PropertyGroup>
```

The generator creates:
- `Identity/ApplicationUser.cs` — extends `IdentityUser` with custom fields
- `Data/ApplicationDbContext.cs` — `IdentityDbContext<ApplicationUser>`
- `AuthManagerSetupHints.cs` — commented Program.cs wiring guide

---

## Configuration Reference

```csharp
options.RoutePrefix           = "authmanager";          // URL path
options.Title                 = "Auth Manager";         // sidebar title
options.DefaultTheme          = AuthManagerTheme.Dark;  // Light | Dark | System
options.RequireAuthentication = true;                   // false = open (dev only!)
options.SuperAdminRole        = "SuperAdmin";           // ONLY this role can access the UI
options.DefaultPageSize       = 25;

// SuperAdmin seeding (Option B — hosted service)
options.SeedSuperAdmin         = true;                  // ⚠️  disable after first login
options.SeedSuperAdminEmail    = "superadmin@example.com";
options.SeedSuperAdminPassword = "SuperAdmin@123456!";

// Password Policy — applied to ASP.NET Identity PasswordOptions at startup
options.PasswordPolicy.MinimumLength          = 8;
options.PasswordPolicy.MaximumLength          = 128;
options.PasswordPolicy.RequireUppercase       = true;
options.PasswordPolicy.RequireLowercase       = true;
options.PasswordPolicy.RequireDigit           = true;
options.PasswordPolicy.RequireNonAlphanumeric = true;
options.PasswordPolicy.PasswordHistoryCount   = 5;   // reject last 5 passwords
options.PasswordPolicy.PasswordExpiryDays     = 90;  // 0 = never
options.PasswordPolicy.DenyUsernameInPassword = true;

// Security / Lockout Policy — applied to ASP.NET Identity LockoutOptions at startup
options.SecurityPolicy.EnableBruteForceDetection = true;
options.SecurityPolicy.MaxFailedLoginAttempts     = 5;
options.SecurityPolicy.LockoutDuration            = TimeSpan.FromMinutes(15);
options.SecurityPolicy.MaxConcurrentSessions      = 0;     // 0 = unlimited
options.SecurityPolicy.InvalidateSessionsOnPasswordChange = true;
options.SecurityPolicy.AllowSelfRegistration      = true;
options.SecurityPolicy.RequireEmailVerificationOnRegistration = false;

// Webhooks — fire-and-forget signed HTTP POSTs on auth events
options.Webhooks.Enabled = true;
options.Webhooks.Endpoints.Add(new WebhookEndpoint
{
    Name   = "My Endpoint",
    Url    = "https://example.com/webhook",
    Secret = "your-hmac-secret",
    Events = [WebhookEventNames.UserCreated, WebhookEventNames.UserLockout]
    // Events = [WebhookEventNames.All]  — subscribe to everything
});

options.Jwt.Issuer                   = "https://api.example.com";
options.Jwt.Audience                 = "https://api.example.com";
options.Jwt.AccessTokenExpiryMinutes = 60;
options.Jwt.EnableRefreshTokens      = true;

options.OAuth.Google.Enabled         = true;
options.OAuth.Google.ClientId        = "...";
options.OAuth.Google.ClientSecret    = "...";

options.OAuth.Microsoft.Enabled      = true;
options.OAuth.Microsoft.TenantId     = "common";

options.LogViewer.MaxLogEntries         = 10_000;
options.LogViewer.LiveUpdateIntervalMs  = 2000;
```

---

## Sample Apps

| App | Location | Description |
|-----|----------|-------------|
| ASP.NET MVC | `samples/AuthManagerSample.Mvc/` | Classic MVC app with Identity + AuthManager admin UI |
| Minimal API | `samples/AuthManagerSample.MinimalApi/` | Minimal API with AuthManager embedded |
| Blazor Server | `samples/AuthManagerSample.BlazorServer/` | Blazor Server app wired to AuthManager |
| **Web API + JWT** | `samples/AuthManagerSample.WebApi/` | .NET 10 REST API with JWT auth, refresh tokens, and AuthManager at `/authmanager` |
| **Blazor Web App** | `samples/AuthManagerSample.BlazorWebApp/` | .NET 10 Blazor Web App (SSR + interactive) with cookie auth, login/register/profile pages, and AuthManager admin panel |

```bash
# Run the JWT Web API sample
cd samples/AuthManagerSample.WebApi
dotnet run
# POST /register  POST /login  GET /me  GET /products/premium
# Open https://localhost:5001/authmanager for the admin UI

# Run the Blazor Web App sample
cd samples/AuthManagerSample.BlazorWebApp
dotnet run
# Open https://localhost:5002
# Register → Login → /profile shows roles, required actions, custom attributes
# Admin users can navigate to /authmanager
```

---

## Publishing to NuGet

### Pack locally

```bash
# 1. Build Release
dotnet build -c Release

# 2. Pack — output goes to ./nupkg/
dotnet pack src/AuthManager.Core/AuthManager.Core.csproj               -c Release -o ./nupkg
dotnet pack src/AuthManager.UI/AuthManager.UI.csproj                   -c Release -o ./nupkg
dotnet pack src/AuthManager.AspNetCore/AuthManager.AspNetCore.csproj   -c Release -o ./nupkg
dotnet pack src/AuthManager.SourceGenerator/AuthManager.SourceGenerator.csproj -c Release -o ./nupkg
```

### Test locally before pushing

```bash
# Add the local folder as a NuGet source
dotnet nuget add source ./nupkg --name local-authmanager

# Install in a test project
dotnet add package DotNetAuthManager --source local-authmanager

# Remove when done
dotnet nuget remove source local-authmanager
```

### Push to NuGet.org

```bash
# Set your API key (get one at https://www.nuget.org/account/apikeys)
export NUGET_API_KEY=your-key-here

dotnet nuget push ./nupkg/DotNetAuthManager.*.nupkg \
  --api-key $NUGET_API_KEY \
  --source https://api.nuget.org/v3/index.json \
  --skip-duplicate
```

### Push to GitHub Packages

```bash
dotnet nuget add source \
  --username YOUR_GITHUB_USERNAME \
  --password $GITHUB_TOKEN \
  --store-password-in-clear-text \
  --name github \
  "https://nuget.pkg.github.com/dotnetappdev/index.json"

dotnet nuget push ./nupkg/DotNetAuthManager.*.nupkg \
  --source github \
  --skip-duplicate
```

### Version bump

Edit `Directory.Build.props` (or each `.csproj`) before packing:

```xml
<PropertyGroup>
  <Version>1.2.0</Version>
  <PackageReleaseNotes>What changed in this release.</PackageReleaseNotes>
</PropertyGroup>
```

---

## Project Structure

```
src/
  AuthManager.Core/           Models, DTOs, service interfaces
  AuthManager.UI/             Blazor RCL (MudBlazor) — pages & layout
  AuthManager.AspNetCore/     DI extensions, service implementations, seeder
  AuthManager.SourceGenerator/ Roslyn scaffolding (optional)
samples/
  AuthManagerSample.Mvc/
  AuthManagerSample.MinimalApi/
  AuthManagerSample.BlazorServer/
  AuthManagerSample.WebApi/        ← .NET 10, JWT REST API
  AuthManagerSample.BlazorWebApp/  ← .NET 10, Blazor Web App
docs/
  site/                       GitHub Pages static site
```

---

## Contributing

PRs welcome. Please open an issue first for large changes.

---

## License

MIT — see [LICENSE](LICENSE).
