# DotNetAuthManager

[![NuGet](https://img.shields.io/nuget/v/DotNetAuthManager.svg)](https://www.nuget.org/packages/DotNetAuthManager)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

A **Keycloak-style** ASP.NET Identity management UI for .NET — inspired by how **.NET Aspire** embeds its dashboard. Drop in a NuGet package, call two methods, and navigate to `/authmanager`.

![Dashboard screenshot](docs/site/assets/img/screenshot-dashboard.png)

---

## Features

| Area | Capability |
|------|-----------|
| **Users** | Full CRUD via MudBlazor DataGrid · Lock/unlock · Password reset · 2FA toggle · Role assignment · Claims editor |
| **Roles** | Create / edit / delete · Assign claims to roles |
| **Claims** | User and role claims management with type reference |
| **JWT** | Configure issuer, audience, expiry, algorithm · Test token generator |
| **OAuth** | Google, Microsoft, Apple, GitHub, custom OIDC providers |
| **Logs** | Real-time Serilog viewer with filtering, search, live mode |
| **Audit** | Every change recorded — who, what, when, from where |
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

    // Seed a default SuperAdmin on first run.
    // ⚠️  Set SeedSuperAdmin = false after first login + password change.
    options.SeedSuperAdmin         = true;
    options.SeedSuperAdminEmail    = "superadmin@example.com";
    options.SeedSuperAdminPassword = "SuperAdmin@123456!";
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthManager();   // → /authmanager
```

### 4. Open the dashboard

Navigate to **`https://localhost:5001/authmanager`**, sign in, change the password, then set `SeedSuperAdmin = false`.

---

## Serilog Integration

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.AuthManager(app.Services)   // feeds the /authmanager/logs viewer
    .CreateLogger();

builder.Host.UseSerilog();
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

// Connection string / provider
options.ConnectionStringName  = "Default";              // reads ConnectionStrings:Default
options.DbProvider            = null;                   // null = auto-detect from connection string

// SuperAdmin seeding (run once, then disable)
options.SeedSuperAdmin         = true;                  // ⚠️  disable after first login
options.SeedSuperAdminEmail    = "superadmin@example.com";
options.SeedSuperAdminPassword = "SuperAdmin@123456!";

options.Jwt.Issuer                  = "https://api.example.com";
options.Jwt.Audience                = "https://api.example.com";
options.Jwt.AccessTokenExpiryMinutes = 60;
options.Jwt.EnableRefreshTokens     = true;

options.OAuth.Google.Enabled        = true;
options.OAuth.Google.ClientId       = "...";
options.OAuth.Google.ClientSecret   = "...";

options.OAuth.Microsoft.Enabled     = true;
options.OAuth.Microsoft.TenantId    = "common";

options.LogViewer.MaxLogEntries     = 10_000;
options.LogViewer.LiveUpdateIntervalMs = 2000;
```

---

## Sample Apps

| App | Location |
|-----|----------|
| ASP.NET MVC | `samples/SampleApp.Mvc/` |
| Minimal API | `samples/SampleApp.MinimalApi/` |
| Blazor Server | `samples/SampleApp.BlazorServer/` |

```bash
cd samples/SampleApp.Mvc
dotnet run
# Open https://localhost:5001/authmanager
```

---

## Project Structure

```
src/
  AuthManager.Core/           Models, DTOs, service interfaces
  AuthManager.UI/             Blazor RCL (MudBlazor) — pages & layout
  AuthManager.AspNetCore/     DI extensions, service implementations
  AuthManager.Storage.SqlServer/
  AuthManager.Storage.PostgreSQL/
  AuthManager.Storage.MySql/
  AuthManager.SourceGenerator/
samples/
  SampleApp.Mvc/
  SampleApp.MinimalApi/
  SampleApp.BlazorServer/
docs/
  site/                       GitHub Pages static site
```

---

## Contributing

PRs welcome. Please open an issue first for large changes.

---

## License

MIT — see [LICENSE](LICENSE).
