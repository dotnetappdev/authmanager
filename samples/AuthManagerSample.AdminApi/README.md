# AuthManagerSample.AdminApi — headless "Web API mode"

Every other sample calls `app.MapAuthManager()`, which maps the Blazor admin UI *and* the
REST API together. This one calls **`app.MapAuthManagerApi()`** instead — no Razor
Components, no MudBlazor, no `_blazor` SignalR hub. The process serves nothing but JSON.

Use this mode when you want AuthManager as a pure identity/admin backend behind your own
frontend — a React/Angular admin console, a mobile app, or another service — the same way
you'd talk to Keycloak's Admin REST API.

## Run it

```bash
cd samples/AuthManagerSample.AdminApi
dotnet run
# Swagger UI: https://localhost:5210/swagger
```

A SuperAdmin account is seeded on first run: `superadmin@example.com` / `SuperAdmin@123456!`.

## Try it

```bash
# 1. Get a token
curl -s -X POST https://localhost:5210/login \
  -H "Content-Type: application/json" \
  -d '{"email":"superadmin@example.com","password":"SuperAdmin@123456!"}'
# → { "accessToken": "eyJ..." }

# 2. Call the admin API with it
curl -s https://localhost:5210/authmanager/api/users \
  -H "Authorization: Bearer eyJ..."
```

## What's mapped

`MapAuthManagerApi()` exposes the same capabilities as the Blazor admin panel, under
`/authmanager/api`: users (CRUD, lock/unlock, roles, claims, required actions, 2FA,
recovery codes, temporary role grants), roles, groups, tenants, sessions, API tokens, and
the audit log — see the main [README](../../README.md#web-api-mode) for the full endpoint
reference.

Every route requires the caller to hold the `SuperAdminRole` (default `"SuperAdmin"`) —
AuthManager doesn't care *how* you authenticated (JWT here, but cookies or an external OIDC
provider work identically), it only checks `ClaimsPrincipal.IsInRole(...)`.

## Going further

- Swap the sample's own `/login` + JWT issuance for your real auth (an existing OIDC
  provider, Entra ID, whatever you already use) — `MapAuthManagerApi()` doesn't know or
  care where the `ClaimsPrincipal` came from.
- Add `app.MapAuthManagerApi()` to an **existing** API you already have — it composes with
  whatever else you're already mapping, same as any other minimal API route group.
- Want the Blazor admin UI too? Call `app.MapAuthManager()` instead (see
  `AuthManagerSample.WebApi`) — that maps the UI *and* this same API together.
