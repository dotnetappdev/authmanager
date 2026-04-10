using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using AuthManagerSample.WebApi.Models;
using AuthManagerSample.WebApi.Services;

namespace AuthManagerSample.WebApi.Endpoints;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/register", Register)
            .WithSummary("Create a new account")
            .AllowAnonymous();

        group.MapPost("/login", Login)
            .WithSummary("Sign in and receive a token pair")
            .AllowAnonymous();

        group.MapPost("/refresh", Refresh)
            .WithSummary("Exchange a refresh token for a new access token")
            .AllowAnonymous();

        group.MapDelete("/logout", Logout)
            .WithSummary("Revoke the refresh token")
            .RequireAuthorization();

        group.MapGet("/me", Me)
            .WithSummary("Get the current user's profile")
            .RequireAuthorization();

        return group;
    }

    // ── handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> Register(
        RegisterRequest req,
        UserManager<IdentityUser> users)
    {
        var existing = await users.FindByEmailAsync(req.Email);
        if (existing is not null)
            return Results.Conflict(new MessageResponse("An account with that email already exists."));

        var user = new IdentityUser
        {
            UserName       = req.DisplayName ?? req.Email.Split('@')[0],
            Email          = req.Email,
            EmailConfirmed = false  // require verification in production
        };

        var result = await users.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return Results.ValidationProblem(
                result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

        return Results.Ok(new MessageResponse(
            $"Account created for {req.Email}. Use POST /api/auth/login to sign in."));
    }

    private static async Task<IResult> Login(
        LoginRequest req,
        TokenService tokens)
    {
        var response = await tokens.IssueTokensAsync(req.Email, req.Password);

        return response is null
            ? Results.Unauthorized()
            : Results.Ok(response);
    }

    private static async Task<IResult> Refresh(
        RefreshRequest req,
        TokenService tokens)
    {
        var response = await tokens.RefreshAsync(req.RefreshToken);

        return response is null
            ? Results.Unauthorized()
            : Results.Ok(response);
    }

    private static IResult Logout(
        RefreshRequest req,
        TokenService tokens)
    {
        tokens.Revoke(req.RefreshToken);
        return Results.Ok(new MessageResponse("Logged out."));
    }

    private static async Task<IResult> Me(
        ClaimsPrincipal principal,
        UserManager<IdentityUser> users)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var user = await users.FindByIdAsync(userId);
        if (user is null) return Results.NotFound();

        var roles    = await users.GetRolesAsync(user);
        var claims   = await users.GetClaimsAsync(user);
        var required = claims
            .Where(c => c.Type == "required_action")
            .Select(c => c.Value)
            .ToList();

        return Results.Ok(new UserInfoResponse(
            UserId:          user.Id,
            Email:           user.Email!,
            UserName:        user.UserName!,
            EmailConfirmed:  user.EmailConfirmed,
            TwoFactorEnabled: user.TwoFactorEnabled,
            Roles:           roles,
            RequiredActions: required));
    }
}
