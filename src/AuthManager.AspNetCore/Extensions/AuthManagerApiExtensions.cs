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

        return app;
    }

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
}
