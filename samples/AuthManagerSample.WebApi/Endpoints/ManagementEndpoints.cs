using Microsoft.AspNetCore.Identity;
using AuthManagerSample.WebApi.Identity;
using AuthManagerSample.WebApi.Models;

namespace AuthManagerSample.WebApi.Endpoints;

public static class ManagementEndpoints
{
    public static RouteGroupBuilder MapManagementEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/roles", GetRoles)
            .WithSummary("List all roles")
            .RequireAuthorization();

        group.MapPost("/roles", CreateRole)
            .WithSummary("Create a role")
            .RequireAuthorization();

        group.MapDelete("/roles/{name}", DeleteRole)
            .WithSummary("Delete a role")
            .RequireAuthorization();

        group.MapGet("/users/{userId}/roles", GetUserRoles)
            .WithSummary("Get roles for a user")
            .RequireAuthorization();

        group.MapPost("/users/{userId}/roles", AddUserRole)
            .WithSummary("Add role to a user")
            .RequireAuthorization();

        group.MapDelete("/users/{userId}/roles/{role}", RemoveUserRole)
            .WithSummary("Remove role from a user")
            .RequireAuthorization();

        group.MapGet("/users/{userId}/claims", GetUserClaims)
            .WithSummary("List a user's claims")
            .RequireAuthorization();

        group.MapPost("/users/{userId}/claims", AddUserClaim)
            .WithSummary("Add a claim to a user")
            .RequireAuthorization();

        group.MapDelete("/users/{userId}/claims", RemoveUserClaim)
            .WithSummary("Remove a claim from a user (type+value in query)")
            .RequireAuthorization();

        return group;
    }

    private static async Task<IResult> GetRoles(RoleManager<IdentityRole> roles)
    {
        var all = await Task.FromResult(roles.Roles.Select(r => r.Name).ToList());
        return Results.Ok(all);
    }

    private static async Task<IResult> CreateRole(CreateRoleRequest req, RoleManager<IdentityRole> roles)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest(new { error = "Name required" });
        var exists = await roles.RoleExistsAsync(req.Name);
        if (exists) return Results.Conflict(new { error = "Role exists" });
        var r = new IdentityRole(req.Name);
        var res = await roles.CreateAsync(r);
        return res.Succeeded ? Results.Created($"/api/admin/roles/{req.Name}", req) : Results.Problem(string.Join(';', res.Errors.Select(e => e.Description)));
    }

    private static async Task<IResult> DeleteRole(string name, RoleManager<IdentityRole> roles)
    {
        var r = await roles.FindByNameAsync(name);
        if (r is null) return Results.NotFound();
        var res = await roles.DeleteAsync(r);
        return res.Succeeded ? Results.Ok() : Results.Problem(string.Join(';', res.Errors.Select(e => e.Description)));
    }

    private static async Task<IResult> GetUserRoles(string userId, UserManager<ApplicationUser> users)
    {
        var u = await users.FindByIdAsync(userId);
        if (u is null) return Results.NotFound();
        var roles = await users.GetRolesAsync(u);
        return Results.Ok(roles);
    }

    private static async Task<IResult> AddUserRole(string userId, AddRoleRequest req, UserManager<ApplicationUser> users)
    {
        var u = await users.FindByIdAsync(userId);
        if (u is null) return Results.NotFound();
        var res = await users.AddToRoleAsync(u, req.Role);
        return res.Succeeded ? Results.Ok() : Results.Problem(string.Join(';', res.Errors.Select(e => e.Description)));
    }

    private static async Task<IResult> RemoveUserRole(string userId, string role, UserManager<ApplicationUser> users)
    {
        var u = await users.FindByIdAsync(userId);
        if (u is null) return Results.NotFound();
        var res = await users.RemoveFromRoleAsync(u, role);
        return res.Succeeded ? Results.Ok() : Results.Problem(string.Join(';', res.Errors.Select(e => e.Description)));
    }

    private static async Task<IResult> GetUserClaims(string userId, UserManager<ApplicationUser> users)
    {
        var u = await users.FindByIdAsync(userId);
        if (u is null) return Results.NotFound();
        var claims = await users.GetClaimsAsync(u);
        return Results.Ok(claims.Select(c => new { c.Type, c.Value }));
    }

    private static async Task<IResult> AddUserClaim(string userId, AddClaimRequest req, UserManager<ApplicationUser> users)
    {
        var u = await users.FindByIdAsync(userId);
        if (u is null) return Results.NotFound();
        var res = await users.AddClaimAsync(u, new System.Security.Claims.Claim(req.Type, req.Value));
        return res.Succeeded ? Results.Ok() : Results.Problem(string.Join(';', res.Errors.Select(e => e.Description)));
    }

    private static async Task<IResult> RemoveUserClaim(string userId, string type, string value, UserManager<ApplicationUser> users)
    {
        var u = await users.FindByIdAsync(userId);
        if (u is null) return Results.NotFound();
        var claim = new System.Security.Claims.Claim(type, value);
        var res = await users.RemoveClaimAsync(u, claim);
        return res.Succeeded ? Results.Ok() : Results.Problem(string.Join(';', res.Errors.Select(e => e.Description)));
    }
}

public record CreateRoleRequest(string Name);
public record AddRoleRequest(string Role);
public record AddClaimRequest(string Type, string Value);
