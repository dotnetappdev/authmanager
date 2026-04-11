---
layout: default
title: AuthManager — Feature walkthrough
---

# AuthManager — Features & Quick Tour

This page explains the key features of the AuthManager sample UI and API. It is suitable for GitHub Pages (Jekyll) and also serves as a standalone Markdown reference.

## Screenshots

- Dashboard: `/assets/images/ui-dashboard.png`
- Users: `/assets/images/ui-users.png`
- Roles: `/assets/images/ui-roles.png`
- Claims: `/assets/images/ui-claims.png`

(Place screenshots in `docs/site/assets/images/` with the filenames above.)

## Features

- User management: create, edit, lock/unlock, reset passwords, bulk import/export.
- Role management: CRUD roles, add/remove users from roles.
- Claims: list, add, remove custom claims for users.
- API Tokens: create API tokens with scopes and expiry.
- Sessions: view and revoke active user sessions.
- Audit log: administrator audit trail for changes.
- Security: configurable password and brute-force protection policies, TOTP support.

## How to run locally

1. Clone repository.
2. Start the WebApi sample (uses SQLite by default):

```bash
dotnet run --project samples/AuthManagerSample.WebApi
```

3. Visit the Swagger UI: `/swagger` and AuthManager UI: `/authmanager`.

## Admin tasks (examples)

- Seeded SuperAdmin credentials (first-run): `superadmin@example.com` / `SuperAdmin@123456!`
- Use the `/api/admin` endpoints (roles/claims) with a JWT from `/api/auth/login`.

## Publishing docs to GitHub Pages

1. In the repository settings, enable GitHub Pages and set source to `docs/site` (or deploy via GitHub Actions).
2. Commit screenshots to `docs/site/assets/images/`.

---

For more detailed API examples, see `samples/AuthManagerSample.WebApi/README.md` in the repo.
