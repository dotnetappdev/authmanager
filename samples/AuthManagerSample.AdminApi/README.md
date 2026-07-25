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
recovery codes, temporary role grants), roles, groups, tenants, sessions, API tokens,
OAuth2 clients, and the audit log — see the main
[README](../../README.md#oauth2-clients-service-to-service-auth) for the full endpoint
reference.

Every route requires the caller to hold the `SuperAdminRole` (default `"SuperAdmin"`) —
AuthManager doesn't care *how* you authenticated (JWT here, but cookies or an external OIDC
provider work identically), it only checks `ClaimsPrincipal.IsInRole(...)` — **except**
`POST /authmanager/api/oauth/token`, which is deliberately anonymous: it's how a client gets
its *first* token in the first place, authenticating via `client_id`/`client_secret` in the
request body per OAuth2 (RFC 6749).

## Service-to-service auth (OAuth2 client-credentials)

This sample's `AddAuthManager()` call sets `options.Jwt.SigningKey` to the **same** secret
its own JWT bearer scheme validates against, so a token issued to a *client* (not a user)
is accepted by this app's own APIs too:

```bash
# Register a client (as the SuperAdmin)
curl -s -X POST https://localhost:5210/authmanager/api/clients \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"clientId":"billing-service","name":"Billing Service","allowedScopes":["read:invoices"]}'
# → { "clientSecret": "cs_...", "client": { ... } }   — secret shown once

# The client gets its own token — no user, no cookie, just its id/secret
curl -s -X POST https://localhost:5210/authmanager/api/oauth/token \
  -d "grant_type=client_credentials&client_id=billing-service&client_secret=cs_..."
# → { "access_token": "eyJ...", "token_type": "Bearer", "expires_in": 3600 }
```

## Going further

- Swap the sample's own `/login` + JWT issuance for your real auth (an existing OIDC
  provider, Entra ID, whatever you already use) — `MapAuthManagerApi()` doesn't know or
  care where the `ClaimsPrincipal` came from.
- Add `app.MapAuthManagerApi()` to an **existing** API you already have — it composes with
  whatever else you're already mapping, same as any other minimal API route group.
- Want the Blazor admin UI too? Call `app.MapAuthManager()` instead (see
  `AuthManagerSample.WebApi`) — that maps the UI *and* this same API together.
