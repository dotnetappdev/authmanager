Guided Setup — Keycloak-like Admin Onboarding

This guide walks you through an opinionated, Keycloak-style setup flow for DotNetAuthManager: database, admin account, basic routes, and optional Docker guidance.

Goals

- Provide a step-by-step onboarding flow for administrators.
- Show how to seed a SuperAdmin and run the UI at `/authmanager`.
- Offer a minimal `docker-compose` example to run a DB for local testing.

1) Choose a database

- Recommended for production: PostgreSQL or SQL Server.
- For quick local testing: SQLite (no server required).

2) Configuration (environment variables)

- `ConnectionStrings__Default` — EF connection string used by sample apps.
- `ASPNETCORE_ENVIRONMENT` — `Development` for local dev.
- `AUTHMANAGER_ROUTE` — optional override for route prefix (default: `authmanager`).

3) Database & Migrations

- If your app uses EF Core migrations, ensure migrations are applied before running the app.

  ```powershell
  dotnet ef database update --project samples/YourSampleProject/YourSampleProject.csproj
  ```

- For SQLite, ensure the file path in your connection string is writable by the app.

4) Seeding the SuperAdmin (Keycloak-like first-user flow)

- Option A — explicit seed call (recommended): In `Program.cs`, after `builder.Build()` call the helper to create a SuperAdmin:

  ```csharp
  await app.CreateDefaultSuperUserAsync<ApplicationUser>(
      email:    "superadmin@example.com",
      password: "SuperAdmin@123456!"
  );
  ```

- Option B — hosted-service seed (auto on startup): Use the `AddAuthManager` options to enable seeding (remember to disable after first run):

  ```csharp
  builder.Services.AddAuthManager<ApplicationUser>(options =>
  {
      options.SeedSuperAdmin = true;
      options.SeedSuperAdminEmail = "superadmin@example.com";
      options.SeedSuperAdminPassword = "SuperAdmin@123456!";
  });
  ```

5) Running locally

- Configure your connection string and run the sample app (one of the projects under `samples/`). Example for the Blazor WebApp sample:

  ```powershell
  cd samples\AuthManagerSample.BlazorWebApp
  dotnet run
  ```

- Open `https://localhost:5001/authmanager` and sign in with the seeded SuperAdmin.

6) Optional: Docker Compose (local dev)

- Minimal `docker-compose.yml` for PostgreSQL (place next to a sample app or in `samples/`):

  ```yaml
  version: '3.8'
  services:
    db:
      image: postgres:15
      environment:
        POSTGRES_USER: authmanager
        POSTGRES_PASSWORD: password
        POSTGRES_DB: authmanager_dev
      ports:
        - "5432:5432"
      volumes:
        - db-data:/var/lib/postgresql/data
  volumes:
    db-data:
  ```

- After starting the DB, update your sample's connection string to point to the Postgres instance and run the sample app locally.

Docker compose location

- The hosted Blazor sample includes a `docker-compose.yml` that spins up Postgres + Adminer at `samples/AuthManagerSample.BlazorHosted/docker-compose.yml`.
- Start it from the `samples/AuthManagerSample.BlazorHosted` folder:

```powershell
docker compose up -d
```

Development `appsettings.Development.json` is provided in the same sample and points to the `db` service so the sample will connect automatically when run with Docker running.

7) Recommended admin UX (Keycloak-like)

- First-run wizard (manual): When no admin exists, redirect `/authmanager` to a setup page that:
  - Prompts for SuperAdmin email + password
  - Connects to selected DB (or shows current DB connection)
  - Runs seeding and creates initial roles (e.g., `SuperAdmin`)

- Alternatively, seed via `CreateDefaultSuperUserAsync` and then show an onboarding checklist in the UI:
  - Configure SMTP for email verification
  - Configure OAuth providers (Google, Microsoft, GitHub)
  - Set password policy
  - Create initial clients/roles

8) Troubleshooting the build error: "The TargetFramework value '' was not recognized."

- If you see this error during `dotnet build`, run a detailed build to find which project lacks a TF:

  ```powershell
  dotnet build -v:detailed
  ```

- Look for lines reporting `TargetFramework=''` in the detailed log and open that `.csproj` to ensure a `<TargetFramework>` or `<TargetFrameworks>` element exists.
- Common causes:
  - A `.props` file overriding or clearing `TargetFramework` via a conditional property group.
  - A generated project file with an empty property substitution (e.g., `<TargetFramework>$(SOME_VAR)</TargetFramework>` where `SOME_VAR` is not set).

9) Next steps I can do for you

- Implement an in-app first-run wizard page and seed endpoint.
- Add the example `docker-compose.yml` into `samples/` and wire a sample app `appsettings.Development.json` to use it.
- Add a short script to run migrations and seed the admin automatically.

NuGet hosting note

The samples demonstrate hosting the `DotNetAuthManager` package in an application. To host the portal online (Okta-like), deploy a web app that references the `DotNetAuthManager` NuGet package and configure production connection strings, persistent storage, HTTPS, and a managed identity or SMTP provider. The same sample setup applies whether you reference the library via project references or via NuGet.

---

Last updated: April 2026
