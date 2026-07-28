using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Extensions;

/// <summary>
/// Maps AuthManager's full admin REST API — the same capabilities as the Blazor admin UI
/// (users, roles, claims, groups, tenants, sessions, API tokens, audit log, health), exposed
/// as JSON endpoints. Modelled on Keycloak's Admin REST API.
///
/// Use this when you want a <b>headless</b> identity backend — build your own frontend (SPA,
/// mobile app, another service) against this API instead of embedding the Blazor admin panel.
/// Requires no MudBlazor / Razor Components at runtime.
///
/// <c>MapAuthManager()</c> calls this internally, so a self-contained host (Blazor UI + API
/// together) doesn't need to call both — call <c>MapAuthManagerApi()</c> alone for API-only
/// ("Web API mode").
/// </summary>
public static class AuthManagerApiExtensions
{
    /// <summary>
    /// Maps the AuthManager admin REST API at <c>/{RoutePrefix}/api</c>. Every route requires
    /// the caller to be authenticated and hold <see cref="AuthManagerOptions.SuperAdminRole"/> —
    /// configure your own authentication (cookies, JWT bearer, etc.) before calling this.
    /// </summary>
    /// <example>
    /// builder.Services.AddAuthentication().AddJwtBearer();
    /// builder.Services.AddAuthManager&lt;ApplicationUser&gt;();
    /// var app = builder.Build();
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.MapAuthManagerApi();   // → /authmanager/api/*, no Blazor UI mapped
    /// app.Run();
    /// </example>
    public static WebApplication MapAuthManagerApi(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<AuthManagerOptions>>().Value;
        var api = app.MapGroup($"/{options.RoutePrefix.Trim('/')}/api")
                      .RequireAuthorization(p => p.RequireRole(options.SuperAdminRole))
                      .WithTags("AuthManager");

        MapHealthEndpoints(api, options);
        MapUserEndpoints(api);
        MapRoleEndpoints(api);
        MapGroupEndpoints(api);
        MapTenantEndpoints(api);
        MapSessionEndpoints(api);
        MapAuditEndpoints(api);
        MapTokenEndpoints(api);
        MapOAuthClientEndpoints(api);
        MapCustomerEndpoints(api);
        MapLicenseEndpoints(api);
        MapCustomerApiKeyEndpoints(api);
        MapSubscriptionEndpoints(api);
        MapPaymentEndpoints(api);
        MapSmsEndpoints(api);

        // OAuth2 client-credentials token endpoint — must stay anonymous (this is how a
        // client obtains its *first* token; it authenticates via client_id/client_secret in
        // the request body, not an existing ClaimsPrincipal), so it overrides the group's
        // RequireAuthorization() explicitly rather than living inside the gated surface.
        app.MapPost($"/{options.RoutePrefix.Trim('/')}/api/oauth/token", async (
                HttpRequest request, IOAuthClientService clients, IJwtConfigService jwt) =>
            {
                if (!request.HasFormContentType)
                    return Results.BadRequest(new { error = "invalid_request", error_description = "Expected application/x-www-form-urlencoded body." });

                var form = await request.ReadFormAsync();
                var grantType = form["grant_type"].ToString();
                if (grantType != "client_credentials")
                    return Results.BadRequest(new { error = "unsupported_grant_type" });

                var clientId = form["client_id"].ToString();
                var clientSecret = form["client_secret"].ToString();
                var client = await clients.ValidateClientCredentialsAsync(clientId, clientSecret);
                if (client is null)
                    return Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized);

                var claims = new List<System.Security.Claims.Claim>
                {
                    new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, client.ClientId),
                    new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                    new("client_id", client.ClientId),
                    new("azp", client.ClientId),
                };
                claims.AddRange(client.AllowedScopes.Select(s => new System.Security.Claims.Claim("scope", s)));

                var expiry = TimeSpan.FromMinutes(options.Jwt.AccessTokenExpiryMinutes);
                var accessToken = await jwt.IssueTokenAsync(claims, expiry);

                return Results.Ok(new
                {
                    access_token = accessToken,
                    token_type = "Bearer",
                    expires_in = (int)expiry.TotalSeconds,
                    scope = string.Join(' ', client.AllowedScopes)
                });
            })
            .AllowAnonymous()
            .WithName("AuthManager.OAuth.Token")
            .WithTags("AuthManager");

        // License validation/activation and customer API key validation are called by the
        // *customer's* own application (a desktop app checking its license, a customer's
        // backend validating a key it was issued) — never by an authenticated admin — so
        // these stay anonymous too, same reasoning as the OAuth2 token endpoint above.
        var licenseRoot = $"/{options.RoutePrefix.Trim('/')}/api/licenses";

        app.MapPost($"{licenseRoot}/validate", async (LicenseValidateRequest req, ILicenseService svc)
                => Results.Ok(await svc.ValidateLicenseAsync(req.Key)))
            .AllowAnonymous().WithName("AuthManager.Licenses.Validate").WithTags("AuthManager");

        app.MapPost($"{licenseRoot}/activate", async (LicenseActivateRequest req, HttpContext ctx, ILicenseService svc) =>
            {
                var ip = ctx.Connection.RemoteIpAddress?.ToString();
                var (ok, errors, result) = await svc.ActivateLicenseAsync(req.Key, req.MachineId, ip);
                return ok ? Results.Ok(result) : Results.BadRequest(new { errors, result });
            })
            .AllowAnonymous().WithName("AuthManager.Licenses.Activate").WithTags("AuthManager");

        app.MapPost($"{licenseRoot}/deactivate", async (LicenseActivateRequest req, ILicenseService svc)
                => (await svc.DeactivateLicenseAsync(req.Key, req.MachineId)).ToResult())
            .AllowAnonymous().WithName("AuthManager.Licenses.Deactivate").WithTags("AuthManager");

        app.MapPost($"/{options.RoutePrefix.Trim('/')}/api/keys/validate", async (ApiKeyValidateRequest req, ICustomerApiKeyService svc) =>
            {
                var key = await svc.ValidateKeyAsync(req.ApiKey);
                return key is null
                    ? Results.Json(new { valid = false }, statusCode: StatusCodes.Status401Unauthorized)
                    : Results.Ok(new { valid = true, key });
            })
            .AllowAnonymous().WithName("AuthManager.CustomerKeys.Validate").WithTags("AuthManager");

        // Passkeys — registration/list/remove require a signed-in user (self-service, no
        // SuperAdmin role needed); the login ceremony is anonymous by nature. Mapped here
        // (not inside the SuperAdmin-gated `api` group above) so headless "Web API mode" apps
        // that only call MapAuthManagerApi() — never MapAuthManager() — still get passkey support.
        MapPasskeyEndpoints(app, options.RoutePrefix.Trim('/'));

        // Stripe/PayPal webhook receivers — these are called by the provider's own servers,
        // never by an AuthManager-authenticated caller, so they stay anonymous and rely on
        // each provider's own signature verification (done inside IPaymentGatewayService)
        // instead of a bearer token.
        MapPaymentWebhookEndpoints(app, options.RoutePrefix.Trim('/'));

        return app;
    }

    private sealed record LicenseValidateRequest(string Key);
    private sealed record LicenseActivateRequest(string Key, string MachineId);
    private sealed record ApiKeyValidateRequest(string ApiKey);

    private static IResult ToResult(this (bool Success, string[] Errors) r)
        => r.Success ? Results.NoContent() : Results.BadRequest(new { errors = r.Errors });

    private static IResult ToResult<T>(this (bool Success, string[] Errors) r, T value)
        => r.Success ? Results.Ok(value) : Results.BadRequest(new { errors = r.Errors });

    // ── Health ───────────────────────────────────────────────────────────────

    private static void MapHealthEndpoints(RouteGroupBuilder api, AuthManagerOptions options)
    {
        api.MapGet("health", () => Results.Ok(new
        {
            Status = "Healthy",
            Service = "DotNetAuthManager",
            Version = typeof(AuthManagerApiExtensions).Assembly.GetName().Version?.ToString(),
            Timestamp = DateTimeOffset.UtcNow
        })).AllowAnonymous().WithName("AuthManager.Health").ExcludeFromDescription();

        api.MapGet("health/report", async (ISystemHealthService svc)
            => Results.Ok(await svc.GetHealthAsync()))
           .WithName("AuthManager.HealthReport");
    }

    // ── Users ────────────────────────────────────────────────────────────────

    private static void MapUserEndpoints(RouteGroupBuilder api)
    {
        var users = api.MapGroup("/users");

        users.MapGet("", async (
            IUserManagementService svc,
            string? search, string? role, bool? locked, bool? emailConfirmed, bool? twoFactor,
            int page = 1, int pageSize = 25, string? sortBy = null, bool sortDescending = false) =>
        {
            var filter = new UserFilter
            {
                SearchTerm = search,
                Role = role,
                IsLockedOut = locked,
                EmailConfirmed = emailConfirmed,
                TwoFactorEnabled = twoFactor,
                Page = page <= 0 ? 1 : page,
                PageSize = pageSize <= 0 ? 25 : pageSize,
                SortBy = string.IsNullOrWhiteSpace(sortBy) ? "UserName" : sortBy,
                SortDescending = sortDescending
            };
            return Results.Ok(await svc.GetUsersAsync(filter));
        }).WithName("AuthManager.Users.List");

        users.MapGet("dashboard-stats", async (IUserManagementService svc)
            => Results.Ok(await svc.GetDashboardStatsAsync())).WithName("AuthManager.Users.DashboardStats");

        users.MapGet("2fa-stats", async (IUserManagementService svc)
            => Results.Ok(await svc.GetTwoFactorStatsAsync())).WithName("AuthManager.Users.TwoFactorStats");

        users.MapGet("{id}", async (string id, IUserManagementService svc)
            => await svc.GetUserByIdAsync(id) is { } u ? Results.Ok(u) : Results.NotFound())
            .WithName("AuthManager.Users.Get");

        users.MapGet("by-email/{email}", async (string email, IUserManagementService svc)
            => await svc.GetUserByEmailAsync(email) is { } u ? Results.Ok(u) : Results.NotFound())
            .WithName("AuthManager.Users.GetByEmail");

        users.MapPost("", async (CreateUserDto dto, IUserManagementService svc) =>
        {
            var (ok, errors) = await svc.CreateUserAsync(dto);
            if (!ok) return Results.BadRequest(new { errors });
            var created = await svc.GetUserByUserNameAsync(dto.UserName);
            return Results.Created($"users/{created?.Id}", created);
        }).WithName("AuthManager.Users.Create");

        users.MapPut("{id}", async (string id, UpdateUserDto dto, IUserManagementService svc) =>
        {
            dto.Id = id;
            return (await svc.UpdateUserAsync(dto)).ToResult();
        }).WithName("AuthManager.Users.Update");

        users.MapDelete("{id}", async (string id, IUserManagementService svc)
            => (await svc.DeleteUserAsync(id)).ToResult()).WithName("AuthManager.Users.Delete");

        users.MapPost("{id}/lock", async (string id, DateTimeOffset? until, IUserManagementService svc)
            => (await svc.LockUserAsync(id, until)).ToResult()).WithName("AuthManager.Users.Lock");

        users.MapPost("{id}/unlock", async (string id, IUserManagementService svc)
            => (await svc.UnlockUserAsync(id)).ToResult()).WithName("AuthManager.Users.Unlock");

        users.MapPost("{id}/reset-password", async (string id, ResetPasswordDto dto, IUserManagementService svc) =>
        {
            dto.UserId = id;
            return (await svc.ResetPasswordAsync(dto)).ToResult();
        }).WithName("AuthManager.Users.ResetPassword");

        users.MapPost("{id}/roles/{role}", async (string id, string role, IUserManagementService svc)
            => (await svc.AssignRoleAsync(id, role)).ToResult()).WithName("AuthManager.Users.AssignRole");

        users.MapDelete("{id}/roles/{role}", async (string id, string role, IUserManagementService svc)
            => (await svc.RemoveRoleAsync(id, role)).ToResult()).WithName("AuthManager.Users.RemoveRole");

        users.MapPost("{id}/roles/{role}/temporary", async (string id, string role, DateTimeOffset expiresAt, IUserManagementService svc)
            => (await svc.AssignTemporaryRoleAsync(id, role, expiresAt)).ToResult()).WithName("AuthManager.Users.AssignTemporaryRole");

        users.MapPost("{id}/roles/{role}/make-permanent", async (string id, string role, IUserManagementService svc)
            => (await svc.MakeRoleAssignmentPermanentAsync(id, role)).ToResult()).WithName("AuthManager.Users.MakeRolePermanent");

        users.MapGet("{id}/roles/expiries", async (string id, IUserManagementService svc)
            => Results.Ok(await svc.GetRoleExpiriesAsync(id))).WithName("AuthManager.Users.RoleExpiries");

        users.MapPost("{id}/claims", async (string id, ClaimDto claim, IUserManagementService svc)
            => (await svc.AddClaimAsync(id, claim)).ToResult()).WithName("AuthManager.Users.AddClaim");

        users.MapDelete("{id}/claims", async (string id, [FromBody] ClaimDto claim, IUserManagementService svc)
            => (await svc.RemoveClaimAsync(id, claim)).ToResult()).WithName("AuthManager.Users.RemoveClaim");

        users.MapGet("{id}/required-actions", async (string id, IUserManagementService svc)
            => Results.Ok(await svc.GetRequiredActionsAsync(id))).WithName("AuthManager.Users.RequiredActions");

        users.MapPost("{id}/required-actions/{action}", async (string id, string action, IUserManagementService svc)
            => (await svc.AddRequiredActionAsync(id, action)).ToResult()).WithName("AuthManager.Users.AddRequiredAction");

        users.MapDelete("{id}/required-actions/{action}", async (string id, string action, IUserManagementService svc)
            => (await svc.RemoveRequiredActionAsync(id, action)).ToResult()).WithName("AuthManager.Users.RemoveRequiredAction");

        users.MapPost("{id}/2fa/disable", async (string id, IUserManagementService svc)
            => (await svc.DisableTwoFactorAsync(id)).ToResult()).WithName("AuthManager.Users.DisableTwoFactor");

        users.MapPost("{id}/2fa/reset", async (string id, IUserManagementService svc)
            => (await svc.ResetAuthenticatorAsync(id)).ToResult()).WithName("AuthManager.Users.ResetAuthenticator");

        users.MapPost("{id}/2fa/force", async (string id, IUserManagementService svc)
            => (await svc.Force2FaEnrollmentAsync(id)).ToResult()).WithName("AuthManager.Users.ForceTwoFactor");

        users.MapPost("{id}/2fa/recovery-codes", async (string id, int? count, IUserManagementService svc) =>
        {
            var (ok, errors, codes) = await svc.GenerateRecoveryCodesAsync(id, count ?? 10);
            return ok ? Results.Ok(new { codes }) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Users.GenerateRecoveryCodes");

        users.MapGet("{id}/2fa/recovery-codes/remaining", async (string id, IUserManagementService svc)
            => Results.Ok(new { remaining = await svc.GetRecoveryCodesRemainingAsync(id) }))
            .WithName("AuthManager.Users.RecoveryCodesRemaining");
    }

    // ── Roles ────────────────────────────────────────────────────────────────

    private static void MapRoleEndpoints(RouteGroupBuilder api)
    {
        var roles = api.MapGroup("/roles");

        roles.MapGet("", async (IRoleManagementService svc)
            => Results.Ok(await svc.GetRolesAsync())).WithName("AuthManager.Roles.List");

        roles.MapGet("{id}", async (string id, IRoleManagementService svc)
            => await svc.GetRoleByIdAsync(id) is { } r ? Results.Ok(r) : Results.NotFound())
            .WithName("AuthManager.Roles.Get");

        roles.MapGet("{id}/users", async (string id, IRoleManagementService svc)
            => await svc.GetRoleByIdAsync(id) is { } r ? Results.Ok(await svc.GetUsersInRoleAsync(r.Name)) : Results.NotFound())
            .WithName("AuthManager.Roles.Users");

        roles.MapPost("", async (CreateRoleDto dto, IRoleManagementService svc) =>
        {
            var (ok, errors) = await svc.CreateRoleAsync(dto);
            if (!ok) return Results.BadRequest(new { errors });
            var created = await svc.GetRoleByNameAsync(dto.Name);
            return Results.Created($"roles/{created?.Id}", created);
        }).WithName("AuthManager.Roles.Create");

        roles.MapPut("{id}", async (string id, UpdateRoleDto dto, IRoleManagementService svc) =>
        {
            dto.Id = id;
            return (await svc.UpdateRoleAsync(dto)).ToResult();
        }).WithName("AuthManager.Roles.Update");

        roles.MapDelete("{id}", async (string id, IRoleManagementService svc)
            => (await svc.DeleteRoleAsync(id)).ToResult()).WithName("AuthManager.Roles.Delete");

        roles.MapPost("{id}/claims", async (string id, ClaimDto claim, IRoleManagementService svc)
            => (await svc.AddClaimToRoleAsync(id, claim)).ToResult()).WithName("AuthManager.Roles.AddClaim");

        roles.MapDelete("{id}/claims", async (string id, [FromBody] ClaimDto claim, IRoleManagementService svc)
            => (await svc.RemoveClaimFromRoleAsync(id, claim)).ToResult()).WithName("AuthManager.Roles.RemoveClaim");
    }

    // ── Groups ───────────────────────────────────────────────────────────────

    private static void MapGroupEndpoints(RouteGroupBuilder api)
    {
        var groups = api.MapGroup("/groups");

        groups.MapGet("", async (IGroupService svc)
            => Results.Ok(await svc.GetGroupsAsync())).WithName("AuthManager.Groups.List");

        groups.MapGet("{id}", async (string id, IGroupService svc)
            => await svc.GetGroupAsync(id) is { } g ? Results.Ok(g) : Results.NotFound())
            .WithName("AuthManager.Groups.Get");

        groups.MapPost("", async (CreateGroupDto dto, IGroupService svc) =>
        {
            var (ok, errors) = await svc.CreateGroupAsync(dto);
            return ok ? Results.Created("groups", null) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Groups.Create");

        groups.MapPut("{id}", async (string id, UpdateGroupDto dto, IGroupService svc)
            => (await svc.UpdateGroupAsync(id, dto)).ToResult()).WithName("AuthManager.Groups.Update");

        groups.MapDelete("{id}", async (string id, IGroupService svc)
            => (await svc.DeleteGroupAsync(id)).ToResult()).WithName("AuthManager.Groups.Delete");

        groups.MapGet("{id}/members", async (string id, IGroupService svc)
            => Results.Ok(await svc.GetGroupMembersAsync(id))).WithName("AuthManager.Groups.Members");

        groups.MapPost("{id}/members/{userId}", async (string id, string userId, IGroupService svc)
            => (await svc.AddUserToGroupAsync(id, userId)).ToResult()).WithName("AuthManager.Groups.AddMember");

        groups.MapDelete("{id}/members/{userId}", async (string id, string userId, IGroupService svc)
            => (await svc.RemoveUserFromGroupAsync(id, userId)).ToResult()).WithName("AuthManager.Groups.RemoveMember");

        api.MapGet("/users/{userId}/groups", async (string userId, IGroupService svc)
            => Results.Ok(await svc.GetUserGroupsAsync(userId))).WithName("AuthManager.Users.Groups");
    }

    // ── Tenants ──────────────────────────────────────────────────────────────

    private static void MapTenantEndpoints(RouteGroupBuilder api)
    {
        var tenants = api.MapGroup("/tenants");

        tenants.MapGet("", async (ITenantService svc)
            => Results.Ok(await svc.GetTenantsAsync())).WithName("AuthManager.Tenants.List");

        tenants.MapGet("{id}", async (string id, ITenantService svc)
            => await svc.GetTenantAsync(id) is { } t ? Results.Ok(t) : Results.NotFound())
            .WithName("AuthManager.Tenants.Get");

        tenants.MapPost("", async (CreateTenantDto dto, ITenantService svc) =>
        {
            var (ok, errors) = await svc.CreateTenantAsync(dto);
            return ok ? Results.Created($"tenants/{dto.Id}", null) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Tenants.Create");

        tenants.MapPut("{id}", async (string id, UpdateTenantDto dto, ITenantService svc)
            => (await svc.UpdateTenantAsync(id, dto)).ToResult()).WithName("AuthManager.Tenants.Update");

        tenants.MapDelete("{id}", async (string id, ITenantService svc)
            => (await svc.DeleteTenantAsync(id)).ToResult()).WithName("AuthManager.Tenants.Delete");

        tenants.MapGet("{id}/members", async (string id, ITenantService svc)
            => Results.Ok(await svc.GetTenantMembersAsync(id))).WithName("AuthManager.Tenants.Members");

        api.MapGet("/users/{userId}/tenant", async (string userId, ITenantService svc)
            => Results.Ok(new { tenantId = await svc.GetUserTenantIdAsync(userId) })).WithName("AuthManager.Users.Tenant");

        api.MapPost("/users/{userId}/tenant/{tenantId}", async (string userId, string tenantId, ITenantService svc)
            => (await svc.AssignUserToTenantAsync(userId, tenantId)).ToResult()).WithName("AuthManager.Users.AssignTenant");

        api.MapDelete("/users/{userId}/tenant", async (string userId, ITenantService svc)
            => (await svc.RemoveUserFromTenantAsync(userId)).ToResult()).WithName("AuthManager.Users.RemoveTenant");
    }

    // ── Sessions ─────────────────────────────────────────────────────────────

    private static void MapSessionEndpoints(RouteGroupBuilder api)
    {
        var sessions = api.MapGroup("/sessions");

        sessions.MapGet("", async (int? page, int? pageSize, ISessionService svc)
            => Results.Ok(await svc.GetAllSessionsAsync(page ?? 1, pageSize ?? 50))).WithName("AuthManager.Sessions.List");

        sessions.MapGet("count", async (ISessionService svc)
            => Results.Ok(new { count = await svc.GetActiveSessionCountAsync() })).WithName("AuthManager.Sessions.Count");

        sessions.MapDelete("{id}", async (string id, ISessionService svc) =>
        {
            await svc.RevokeSessionAsync(id);
            return Results.NoContent();
        }).WithName("AuthManager.Sessions.Revoke");

        api.MapGet("/users/{userId}/sessions", async (string userId, ISessionService svc)
            => Results.Ok(await svc.GetUserSessionsAsync(userId))).WithName("AuthManager.Users.Sessions");

        api.MapDelete("/users/{userId}/sessions", async (string userId, ISessionService svc) =>
        {
            await svc.RevokeAllUserSessionsAsync(userId);
            return Results.NoContent();
        }).WithName("AuthManager.Users.RevokeAllSessions");
    }

    // ── Audit ────────────────────────────────────────────────────────────────

    private static void MapAuditEndpoints(RouteGroupBuilder api)
    {
        var audit = api.MapGroup("/audit");

        audit.MapGet("", async (int? page, int? pageSize, IAuditService svc)
            => Results.Ok(await svc.GetAuditLogAsync(page ?? 1, pageSize ?? 50))).WithName("AuthManager.Audit.List");

        audit.MapGet("export", async (IAuditService svc) =>
        {
            var bytes = await svc.ExportAuditLogCsvAsync();
            return Results.File(bytes, "text/csv", $"audit-log-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv");
        }).WithName("AuthManager.Audit.Export");
    }

    // ── API Tokens ───────────────────────────────────────────────────────────

    private static void MapTokenEndpoints(RouteGroupBuilder api)
    {
        var tokens = api.MapGroup("/tokens");

        tokens.MapGet("", async (string? userId, IApiTokenService svc)
            => Results.Ok(await svc.GetTokensAsync(userId))).WithName("AuthManager.Tokens.List");

        tokens.MapPost("", async (CreateApiTokenDto dto, IApiTokenService svc) =>
        {
            var (ok, errors, result) = await svc.CreateTokenAsync(dto);
            return ok ? Results.Ok(result) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Tokens.Create");

        tokens.MapPost("{id}/revoke", async (string id, IApiTokenService svc)
            => (await svc.RevokeTokenAsync(id)).ToResult()).WithName("AuthManager.Tokens.Revoke");

        tokens.MapDelete("{id}", async (string id, IApiTokenService svc)
            => (await svc.DeleteTokenAsync(id)).ToResult()).WithName("AuthManager.Tokens.Delete");
    }

    // ── OAuth2 Clients ───────────────────────────────────────────────────────

    private static void MapOAuthClientEndpoints(RouteGroupBuilder api)
    {
        var clients = api.MapGroup("/clients");

        clients.MapGet("", async (IOAuthClientService svc)
            => Results.Ok(await svc.GetClientsAsync())).WithName("AuthManager.Clients.List");

        clients.MapGet("{id}", async (string id, IOAuthClientService svc)
            => await svc.GetClientAsync(id) is { } c ? Results.Ok(c) : Results.NotFound())
            .WithName("AuthManager.Clients.Get");

        clients.MapPost("", async (CreateOAuthClientDto dto, IOAuthClientService svc) =>
        {
            var (ok, errors, result) = await svc.CreateClientAsync(dto);
            return ok ? Results.Created($"clients/{result!.Client.Id}", result) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Clients.Create");

        clients.MapPut("{id}", async (string id, UpdateOAuthClientDto dto, IOAuthClientService svc)
            => (await svc.UpdateClientAsync(id, dto)).ToResult()).WithName("AuthManager.Clients.Update");

        clients.MapDelete("{id}", async (string id, IOAuthClientService svc)
            => (await svc.DeleteClientAsync(id)).ToResult()).WithName("AuthManager.Clients.Delete");

        clients.MapPost("{id}/regenerate-secret", async (string id, IOAuthClientService svc) =>
        {
            var (ok, errors, secret) = await svc.RegenerateSecretAsync(id);
            return ok ? Results.Ok(new { clientSecret = secret }) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Clients.RegenerateSecret");
    }

    // ── Customers ────────────────────────────────────────────────────────────

    private static void MapCustomerEndpoints(RouteGroupBuilder api)
    {
        var customers = api.MapGroup("/customers");

        customers.MapGet("", async (string? search, ICustomerService svc)
            => Results.Ok(await svc.GetCustomersAsync(search))).WithName("AuthManager.Customers.List");

        customers.MapGet("{id}", async (string id, ICustomerService svc)
            => await svc.GetCustomerAsync(id) is { } c ? Results.Ok(c) : Results.NotFound())
            .WithName("AuthManager.Customers.Get");

        customers.MapPost("", async (CreateCustomerDto dto, ICustomerService svc) =>
        {
            var (ok, errors, customer) = await svc.CreateCustomerAsync(dto);
            return ok ? Results.Created($"customers/{customer!.Id}", customer) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Customers.Create");

        customers.MapPut("{id}", async (string id, UpdateCustomerDto dto, ICustomerService svc)
            => (await svc.UpdateCustomerAsync(id, dto)).ToResult()).WithName("AuthManager.Customers.Update");

        customers.MapDelete("{id}", async (string id, ICustomerService svc)
            => (await svc.DeleteCustomerAsync(id)).ToResult()).WithName("AuthManager.Customers.Delete");
    }

    // ── License Keys ─────────────────────────────────────────────────────────

    private static void MapLicenseEndpoints(RouteGroupBuilder api)
    {
        var licenses = api.MapGroup("/licenses");

        licenses.MapGet("", async (string? customerId, ILicenseService svc)
            => Results.Ok(await svc.GetLicensesAsync(customerId))).WithName("AuthManager.Licenses.List");

        licenses.MapGet("{id}", async (string id, ILicenseService svc)
            => await svc.GetLicenseAsync(id) is { } l ? Results.Ok(l) : Results.NotFound())
            .WithName("AuthManager.Licenses.Get");

        licenses.MapGet("{id}/activations", async (string id, ILicenseService svc)
            => Results.Ok(await svc.GetActivationsAsync(id))).WithName("AuthManager.Licenses.Activations");

        licenses.MapPost("", async (CreateLicenseKeyDto dto, ILicenseService svc) =>
        {
            var (ok, errors, license) = await svc.CreateLicenseAsync(dto);
            return ok ? Results.Created($"licenses/{license!.Id}", license) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Licenses.Create");

        licenses.MapPut("{id}", async (string id, UpdateLicenseKeyDto dto, ILicenseService svc)
            => (await svc.UpdateLicenseAsync(id, dto)).ToResult()).WithName("AuthManager.Licenses.Update");

        licenses.MapPost("{id}/revoke", async (string id, ILicenseService svc)
            => (await svc.RevokeLicenseAsync(id)).ToResult()).WithName("AuthManager.Licenses.Revoke");

        licenses.MapDelete("{id}", async (string id, ILicenseService svc)
            => (await svc.DeleteLicenseAsync(id)).ToResult()).WithName("AuthManager.Licenses.Delete");
    }

    // ── Customer API Keys ────────────────────────────────────────────────────

    private static void MapCustomerApiKeyEndpoints(RouteGroupBuilder api)
    {
        var keys = api.MapGroup("/keys");

        keys.MapGet("", async (string? customerId, ICustomerApiKeyService svc)
            => Results.Ok(await svc.GetKeysAsync(customerId))).WithName("AuthManager.CustomerKeys.List");

        keys.MapGet("{id}", async (string id, ICustomerApiKeyService svc)
            => await svc.GetKeyAsync(id) is { } k ? Results.Ok(k) : Results.NotFound())
            .WithName("AuthManager.CustomerKeys.Get");

        keys.MapPost("", async (CreateCustomerApiKeyDto dto, ICustomerApiKeyService svc) =>
        {
            var (ok, errors, result) = await svc.CreateKeyAsync(dto);
            return ok ? Results.Ok(result) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.CustomerKeys.Create");

        keys.MapPut("{id}", async (string id, UpdateCustomerApiKeyDto dto, ICustomerApiKeyService svc)
            => (await svc.UpdateKeyAsync(id, dto)).ToResult()).WithName("AuthManager.CustomerKeys.Update");

        keys.MapPost("{id}/revoke", async (string id, ICustomerApiKeyService svc)
            => (await svc.RevokeKeyAsync(id)).ToResult()).WithName("AuthManager.CustomerKeys.Revoke");

        keys.MapPost("{id}/regenerate", async (string id, ICustomerApiKeyService svc) =>
        {
            var (ok, errors, newKey) = await svc.RegenerateKeyAsync(id);
            return ok ? Results.Ok(new { apiKey = newKey }) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.CustomerKeys.Regenerate");

        keys.MapDelete("{id}", async (string id, ICustomerApiKeyService svc)
            => (await svc.DeleteKeyAsync(id)).ToResult()).WithName("AuthManager.CustomerKeys.Delete");
    }

    // ── Subscriptions ────────────────────────────────────────────────────────

    private static void MapSubscriptionEndpoints(RouteGroupBuilder api)
    {
        var plans = api.MapGroup("/subscription-plans");

        plans.MapGet("", async (ISubscriptionService svc)
            => Results.Ok(await svc.GetPlansAsync())).WithName("AuthManager.Plans.List");

        plans.MapGet("{id}", async (string id, ISubscriptionService svc)
            => await svc.GetPlanAsync(id) is { } p ? Results.Ok(p) : Results.NotFound())
            .WithName("AuthManager.Plans.Get");

        plans.MapPost("", async (CreateSubscriptionPlanDto dto, ISubscriptionService svc) =>
        {
            var (ok, errors, plan) = await svc.CreatePlanAsync(dto);
            return ok ? Results.Created($"subscription-plans/{plan!.Id}", plan) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Plans.Create");

        plans.MapPut("{id}", async (string id, UpdateSubscriptionPlanDto dto, ISubscriptionService svc)
            => (await svc.UpdatePlanAsync(id, dto)).ToResult()).WithName("AuthManager.Plans.Update");

        plans.MapDelete("{id}", async (string id, ISubscriptionService svc)
            => (await svc.DeletePlanAsync(id)).ToResult()).WithName("AuthManager.Plans.Delete");

        var subs = api.MapGroup("/subscriptions");

        subs.MapGet("", async (string? customerId, ISubscriptionService svc)
            => Results.Ok(await svc.GetSubscriptionsAsync(customerId))).WithName("AuthManager.Subscriptions.List");

        subs.MapGet("{id}", async (string id, ISubscriptionService svc)
            => await svc.GetSubscriptionAsync(id) is { } s ? Results.Ok(s) : Results.NotFound())
            .WithName("AuthManager.Subscriptions.Get");

        subs.MapPost("", async (CreateCustomerSubscriptionDto dto, ISubscriptionService svc) =>
        {
            var (ok, errors, sub) = await svc.SubscribeAsync(dto);
            return ok ? Results.Created($"subscriptions/{sub!.Id}", sub) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Subscriptions.Create");

        subs.MapPost("{id}/change-plan", async (string id, ChangeSubscriptionPlanDto dto, ISubscriptionService svc)
            => (await svc.ChangePlanAsync(id, dto)).ToResult()).WithName("AuthManager.Subscriptions.ChangePlan");

        subs.MapPost("{id}/cancel", async (string id, ISubscriptionService svc)
            => (await svc.CancelSubscriptionAsync(id)).ToResult()).WithName("AuthManager.Subscriptions.Cancel");

        subs.MapPost("{id}/reactivate", async (string id, ISubscriptionService svc)
            => (await svc.ReactivateSubscriptionAsync(id)).ToResult()).WithName("AuthManager.Subscriptions.Reactivate");

        api.MapGet("/customers/{customerId}/subscription", async (string customerId, ISubscriptionService svc)
            => await svc.GetActiveSubscriptionForCustomerAsync(customerId) is { } s ? Results.Ok(s) : Results.NotFound())
            .WithName("AuthManager.Customers.ActiveSubscription");
    }

    // ── Payments (Stripe / PayPal) ──────────────────────────────────────────────

    private static void MapPaymentEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/payment-settings", async (IPaymentSettingsService svc)
            => Results.Ok(await svc.GetSettingsAsync())).WithName("AuthManager.Payments.GetSettings");

        api.MapPut("/payment-settings", async (UpdatePaymentSettingsDto dto, IPaymentSettingsService svc) =>
        {
            await svc.UpdateSettingsAsync(dto);
            return Results.NoContent();
        }).WithName("AuthManager.Payments.UpdateSettings");

        api.MapPost("/payments/stripe/checkout", async (PaymentCheckoutRequest req, IPaymentGatewayService svc) =>
        {
            var (ok, errors, url) = await svc.CreateStripeCheckoutSessionAsync(req.CustomerId, req.PlanId);
            return ok ? Results.Ok(new { checkoutUrl = url }) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Payments.StripeCheckout");

        api.MapPost("/payments/paypal/checkout", async (PaymentCheckoutRequest req, IPaymentGatewayService svc) =>
        {
            var (ok, errors, url) = await svc.CreatePayPalCheckoutAsync(req.CustomerId, req.PlanId);
            return ok ? Results.Ok(new { approvalUrl = url }) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Payments.PayPalCheckout");

        api.MapPost("/payments/paypal/orders/{orderId}/capture", async (string orderId, IPaymentGatewayService svc)
            => (await svc.HandlePayPalOrderApprovedAsync(orderId)).ToResult())
            .WithName("AuthManager.Payments.PayPalCaptureOrder");

        api.MapPost("/payments/paypal/subscriptions/{id}/confirm", async (string id, IPaymentGatewayService svc)
            => (await svc.HandlePayPalSubscriptionApprovedAsync(id)).ToResult())
            .WithName("AuthManager.Payments.PayPalConfirmSubscription");
    }

    private sealed record PaymentCheckoutRequest(string CustomerId, string PlanId);

    // ── SMS (OTP delivery — Twilio / Vonage / MessageBird / Sinch / Textlocal) ──────────────

    private static void MapSmsEndpoints(RouteGroupBuilder api)
    {
        api.MapGet("/sms-settings", async (ISmsSettingsService svc)
            => Results.Ok(await svc.GetSettingsAsync())).WithName("AuthManager.Sms.GetSettings");

        api.MapPut("/sms-settings", async (UpdateSmsSettingsDto dto, ISmsSettingsService svc) =>
        {
            await svc.UpdateSettingsAsync(dto);
            return Results.NoContent();
        }).WithName("AuthManager.Sms.UpdateSettings");

        api.MapPost("/sms/send-test", async (SendTestSmsRequest req, ISmsSenderService svc) =>
        {
            var (ok, errors) = await svc.SendAsync(req.PhoneNumber, req.Message);
            return ok ? Results.Ok(new { message = "Test message sent." }) : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Sms.SendTest");
    }

    private sealed record SendTestSmsRequest(string PhoneNumber, string Message);

    // ── Passkeys ─────────────────────────────────────────────────────────────

    // ── Payment webhooks (anonymous — verified via provider signature, not a bearer token) ──

    private static void MapPaymentWebhookEndpoints(WebApplication app, string prefix)
    {
        app.MapPost($"/{prefix}/api/webhooks/stripe", async (HttpRequest request, IPaymentGatewayService svc) =>
        {
            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync();
            var signature = request.Headers["Stripe-Signature"].ToString();
            var (ok, errors) = await svc.HandleStripeWebhookAsync(payload, signature);
            return ok ? Results.Ok() : Results.BadRequest(new { errors });
        }).AllowAnonymous().WithName("AuthManager.Payments.StripeWebhook").WithTags("AuthManager");

        app.MapPost($"/{prefix}/api/webhooks/paypal", async (HttpRequest request, IPaymentGatewayService svc) =>
        {
            using var reader = new StreamReader(request.Body);
            var payload = await reader.ReadToEndAsync();
            var headers = request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
            var (ok, errors) = await svc.HandlePayPalWebhookAsync(payload, headers);
            return ok ? Results.Ok() : Results.BadRequest(new { errors });
        }).AllowAnonymous().WithName("AuthManager.Payments.PayPalWebhook").WithTags("AuthManager");
    }

    private static void MapPasskeyEndpoints(WebApplication app, string prefix)
    {
        var self = app.MapGroup($"/{prefix}/api/passkeys").RequireAuthorization().WithTags("AuthManager.Passkeys");

        self.MapGet("", async (HttpContext ctx, IPasskeyService svc) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            return Results.Ok(await svc.GetPasskeysAsync(userId));
        }).WithName("AuthManager.Passkeys.List");

        self.MapGet("creation-options", async (HttpContext ctx, IPasskeyService svc) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            var options = await svc.GetCreationOptionsAsync(userId);
            return options is null ? Results.NotFound() : Results.Content(options, "application/json");
        }).WithName("AuthManager.Passkeys.CreationOptions");

        self.MapPost("register", async (HttpContext ctx, PasskeyRegisterRequest req, IPasskeyService svc) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            var (ok, error) = await svc.CompleteRegistrationAsync(userId, req.CredentialJson);
            return ok ? Results.NoContent() : Results.BadRequest(new { error });
        }).WithName("AuthManager.Passkeys.Register");

        self.MapDelete("{credentialId}", async (string credentialId, HttpContext ctx, IPasskeyService svc) =>
        {
            var userId = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId is null) return Results.Unauthorized();
            var (ok, errors) = await svc.RemovePasskeyAsync(userId, credentialId);
            return ok ? Results.NoContent() : Results.BadRequest(new { errors });
        }).WithName("AuthManager.Passkeys.Remove");

        // Login ceremony — anonymous, same reasoning as the OAuth2 token endpoint: the caller
        // isn't authenticated yet, that's precisely what this establishes.
        var login = app.MapGroup($"/{prefix}/api/passkeys/login").AllowAnonymous().WithTags("AuthManager.Passkeys");

        login.MapGet("options", async (string? username, IPasskeyService svc)
            => Results.Content(await svc.GetRequestOptionsAsync(username), "application/json"))
            .WithName("AuthManager.Passkeys.LoginOptions");

        login.MapPost("", async (PasskeyLoginRequest req, IPasskeyService svc) =>
        {
            var outcome = await svc.SignInAsync(req.CredentialJson);
            return outcome switch
            {
                PasskeySignInOutcome.Succeeded => Results.Ok(new { outcome = outcome.ToString() }),
                PasskeySignInOutcome.RequiresTwoFactor => Results.Ok(new { outcome = outcome.ToString() }),
                PasskeySignInOutcome.LockedOut => Results.Json(new { outcome = outcome.ToString() }, statusCode: StatusCodes.Status423Locked),
                _ => Results.Json(new { outcome = outcome.ToString() }, statusCode: StatusCodes.Status401Unauthorized)
            };
        }).WithName("AuthManager.Passkeys.Login");
    }

    private sealed record PasskeyRegisterRequest(string CredentialJson);
    private sealed record PasskeyLoginRequest(string CredentialJson);
}
