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
DotNetAuthManager (main)
├── AuthManager.Core          — Models, DTOs, service interfaces
├── AuthManager.UI            — Blazor Server RCL (MudBlazor)
└── AuthManager.AspNetCore    — DI extensions, middleware, services

Storage (pick one):
├── DotNetAuthManager.Storage.SqlServer    — SQL Server via EF Core
├── DotNetAuthManager.Storage.PostgreSQL   — PostgreSQL via Npgsql
└── DotNetAuthManager.Storage.MySql        — MySQL/MariaDB via Pomelo

Tooling:
└── DotNetAuthManager.SourceGenerator — Roslyn scaffolding
```

---

## Quick Start

### 1. Install the core package

```bash
dotnet add package DotNetAuthManager
```

Then install one EF Core provider for your database:

```bash
dotnet add package Microsoft.EntityFrameworkCore.SqlServer     # SQL Server
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL       # PostgreSQL
dotnet add package Pomelo.EntityFrameworkCore.MySql            # MySQL/MariaDB
dotnet add package Microsoft.EntityFrameworkCore.Sqlite        # SQLite
```

### 2. Add connection string to appsettings.json

```json
{
  "ConnectionStrings": {
    "Default": "Server=.;Database=MyApp;Trusted_Connection=True;"
  }
}
```

The package reads this automatically — **no need to pass it in code**.
Provider (SQL Server / PostgreSQL / MySQL / SQLite) is auto-detected from the connection string format.

### 3. Program.cs — two calls

```csharp
// ── Services ──────────────────────────────────────────────
builder.Services.AddAuthManager<ApplicationUser>(
    builder.Configuration,          // reads ConnectionStrings:Default, auto-detects provider
    options =>
    {
        options.RoutePrefix    = "authmanager";
        options.DefaultTheme   = AuthManagerTheme.Dark;

        // Only users with the SuperAdmin role can access the management UI.
        options.SuperAdminRole = "SuperAdmin";

        // Seed a default SuperAdmin on first run. Remove once set up!
        options.SeedSuperAdmin         = true;
        options.SeedSuperAdminEmail    = "superadmin@example.com";
        options.SeedSuperAdminPassword = "SuperAdmin@123456!";
    },
    identity =>
    {
        identity.Password.RequiredLength        = 8;
        identity.Lockout.MaxFailedAccessAttempts = 5;
        identity.User.RequireUniqueEmail        = true;
    }
);

// ── Pipeline ──────────────────────────────────────────────
var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthManager();       // → /authmanager, SuperAdmin only
```

### 4. Open the dashboard

Navigate to **`https://localhost:5001/authmanager`** and sign in with the seeded SuperAdmin account.
Change the password immediately, then set `options.SeedSuperAdmin = false`.

---

## How provider detection works

The package **scans every entry** in `ConnectionStrings` and picks the first one it recognises. You never need to specify a name or a provider — it just works.

| Pattern in any connection string | Detected provider |
|----------------------------------|-------------------|
| `Server=.;Database=…;Trusted_Connection=True` | SQL Server |
| `Host=localhost;Username=…` | PostgreSQL |
| `server=localhost;uid=…` | MySQL/MariaDB |
| `Data Source=app.db` | SQLite |

Multiple connection strings? Set the provider in one line and it picks the matching one:

```csharp
options.DbProvider = AuthManagerDbProvider.PostgreSQL;  // picks the PostgreSQL string
```

## Using Your Own DbContext

If you already have Identity set up (called `AddIdentity()` + `AddEntityFrameworkStores()`):

```csharp
// Just layer AuthManager on top — no connection string needed
builder.Services.AddAuthManager<ApplicationUser>(options =>
{
    options.RoutePrefix    = "authmanager";
    options.SuperAdminRole = "SuperAdmin";
});

app.MapAuthManager();
```

## Different connection string name

```json
{
  "ConnectionStrings": {
    "IdentityDb": "Host=db;Database=myapp;Username=app;Password=secret"
  }
}
```

```csharp
builder.Services.AddAuthManager<ApplicationUser>(builder.Configuration, options =>
{
    options.ConnectionStringName = "IdentityDb";   // reads that key
    options.RoutePrefix = "authmanager";
});
```

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
