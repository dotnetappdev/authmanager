using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using AuthManagerSample.WebApi.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using AuthManagerSample.WebApi.Models;

namespace AuthManagerSample.WebApi.Services;

/// <summary>
/// Issues and validates JWT access tokens + opaque refresh tokens.
/// Refresh tokens are kept in memory — replace the store with Redis or a DB table
/// for multi-node deployments.
/// </summary>
public sealed class TokenService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly JwtOptions _opts;
    private readonly SymmetricSecurityKey _signingKey;

    // In-memory refresh token store: token → userId
    private readonly ConcurrentDictionary<string, string> _refreshTokens = new();

    public TokenService(UserManager<ApplicationUser> users, IOptions<JwtOptions> opts)
    {
        _users = users;
        _opts  = opts.Value;
        _signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_opts.SecretKey));
    }

    /// <summary>
    /// Verify credentials and issue a token pair.
    /// Returns null if the credentials are invalid or the account is locked.
    /// </summary>
    public async Task<TokenResponse?> IssueTokensAsync(string email, string password)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user is null) return null;

        if (!await _users.CheckPasswordAsync(user, password)) return null;

        return await BuildTokenPairAsync(user);
    }

    /// <summary>Rotate a refresh token for a new access token.</summary>
    public async Task<TokenResponse?> RefreshAsync(string refreshToken)
    {
        if (!_refreshTokens.TryRemove(refreshToken, out var userId))
            return null;

        var user = await _users.FindByIdAsync(userId);
        if (user is null) return null;

        return await BuildTokenPairAsync(user);
    }

    /// <summary>Revoke a refresh token (logout).</summary>
    public void Revoke(string refreshToken) => _refreshTokens.TryRemove(refreshToken, out _);

    // ── internals ─────────────────────────────────────────────────────────────

    private async Task<TokenResponse> BuildTokenPairAsync(ApplicationUser user)
    {
        var roles  = await _users.GetRolesAsync(user);
        var claims = await _users.GetClaimsAsync(user);

        var jwtClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,   DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
        };

        jwtClaims.AddRange(roles.Select(r  => new Claim(ClaimTypes.Role, r)));
        jwtClaims.AddRange(claims.Where(c => c.Type != "password_history")   // never leak history
                                  .Select(c => new Claim(c.Type, c.Value)));

        var token = new JwtSecurityToken(
            issuer:             _opts.Issuer,
            audience:           _opts.Audience,
            claims:             jwtClaims,
            expires:            DateTime.UtcNow.AddMinutes(_opts.AccessTokenExpiryMinutes),
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        var accessToken  = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateRefreshToken();

        _refreshTokens[refreshToken] = user.Id;

        return new TokenResponse(
            AccessToken:  accessToken,
            RefreshToken: refreshToken,
            ExpiresIn:    _opts.AccessTokenExpiryMinutes * 60);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

/// <summary>JWT settings — configure in appsettings.json under "Jwt".</summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string SecretKey  { get; set; } = string.Empty;
    public string Issuer     { get; set; } = string.Empty;
    public string Audience   { get; set; } = string.Empty;
    public int AccessTokenExpiryMinutes { get; set; } = 60;
}
