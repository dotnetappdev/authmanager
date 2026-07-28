# AuthManagerSample.WebApi

Quick start (zero-config)

Prerequisites:
- .NET 10 SDK

Run the Web API sample (uses SQLite by default):

```bash
dotnet run --project samples/AuthManagerSample.WebApi
```

One-command start (recommended)

On Windows (PowerShell):

```powershell
.\scripts\run-webapi.ps1
```

On macOS / Linux (bash):

```bash
./scripts/run-webapi.sh
```

What this does:
- Creates `webapi.db` in the sample folder (SQLite).
- Runs DB creation and seeds a `SuperAdmin` user automatically.

Defaults (first-run seeded credentials):
- Email: `superadmin@example.com`
- Password: `SuperAdmin@123456!`

Endpoints of interest:
- Swagger UI: `https://localhost:5001/swagger` (or `http://localhost:5000/swagger`)
- AuthManager UI: `https://localhost:5001/authmanager` (SuperAdmin only)
- Management endpoints (roles/claims): `https://localhost:5001/api/admin/...` (requires SuperAdmin role)

How to obtain a JWT to call admin endpoints via Swagger:
1. POST `/api/auth/login` with JSON body:

```json
{ "email": "superadmin@example.com", "password": "SuperAdmin@123456!" }
```

2. Copy the `accessToken` from the response and click Authorize in Swagger, then use `Bearer <accessToken>`.

Notes:
- The sample prefers the `DefaultSQLite` connection string (appsettings.json). If you want to use LocalDB/SQL Server, update `ConnectionStrings:Default` or remove `DefaultSQLite`.
- If you want the server to listen on specific URLs, pass `--urls` to `dotnet run` or set `ASPNETCORE_URLS`.

Demo seed data
- On first run, `DemoSeeder` (`Data/DemoSeeder.cs`) creates four Identity accounts beyond the
  SuperAdmin — one each in the `Admin`, `Customer`, `Reader`, and `Viewer` roles
  (`admin@example.com` / `Admin@123456!`, `customer@example.com` / `Customer@123456!`,
  `reader@example.com` / `Reader@123456!`, `viewer@example.com` / `Viewer@123456!`) — plus demo
  rows in the licensing entities: 3 customers, 3 license keys, 2 subscription plans with active
  subscriptions, 1 customer API key, and 2 OAuth clients. It's idempotent — safe to leave running
  on every startup, it only creates what's missing.
- To clear just the licensing demo rows (customers, license keys, subscriptions, API keys, OAuth
  clients) without touching any Identity account or password — SuperAdmin included — call:

  ```bash
  curl -s -X POST -H "Authorization: Bearer $TOKEN" https://localhost:5001/api/admin/purge-demo-data
  ```

  Restart the app afterwards (or call `DemoSeeder.SeedAsync` yourself) to repopulate the
  licensing demo rows — the Identity accounts won't be recreated since they still exist.

Quick example calls (Swagger / curl)

1) Obtain JWT (login):

```bash
curl -s -X POST https://localhost:5001/api/auth/login \
	-H "Content-Type: application/json" \
	-d '{"email":"superadmin@example.com","password":"SuperAdmin@123456!"}' | jq .
```

Response contains `accessToken` — use it for admin calls.

2) List roles using curl:

```bash
TOKEN="<accessToken>"
curl -s -H "Authorization: Bearer $TOKEN" https://localhost:5001/api/admin/roles | jq .
```

3) Create a role:

```bash
curl -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
	-d '{"name":"Support"}' https://localhost:5001/api/admin/roles
```

4) Add role to user:

```bash
curl -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
	-d '{"role":"Support"}' https://localhost:5001/api/admin/users/<userId>/roles
```

5) List user claims:

```bash
curl -H "Authorization: Bearer $TOKEN" https://localhost:5001/api/admin/users/<userId>/claims | jq .
```

Screenshots and in-app walkthrough

I added placeholders for screenshots below — capture the running app pages and drop PNG files into the repo at `docs/site/assets/images/` with these names:

- `ui-dashboard.png` — main AuthManager UI dashboard
- `ui-users.png` — user list page
- `ui-roles.png` — roles page
- `ui-claims.png` — claims page

Instructions to capture and add screenshots:

1. Start the WebApi sample:

```bash
dotnet run --project samples/AuthManagerSample.WebApi
```

2. Open `https://localhost:5001/authmanager` and sign in with seeded SuperAdmin credentials.
3. On Windows: use Snipping Tool or `Win+Shift+S` to capture; on macOS: `Cmd+Shift+4`.
4. Save PNGs into `docs/site/assets/images/` and commit.

Once images are placed the GitHub Pages site (docs/site) will show them automatically.
