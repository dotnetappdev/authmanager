using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthManagerSample.AdminApi.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthManagerSample.AdminApi.Services;

/// <summary>
/// Issues JWT access tokens for this sample's own /login endpoint. AuthManager itself does not
/// issue tokens for the admin API — bring whatever authentication scheme you already use (JWT
/// bearer here, but cookies or an external OIDC provider work identically) and AuthManager's
/// [RequireRole(SuperAdminRole)] check just reads the resulting ClaimsPrincipal.
/// </summary>
public sealed class TokenService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly JwtOptions _opts;
    private readonly SymmetricSecurityKey _signingKey;

    public TokenService(UserManager<ApplicationUser> users, IOptions<JwtOptions> opts)
    {
        _users = users;
        _opts = opts.Value;
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SecretKey));
    }

    public async Task<string?> IssueAccessTokenAsync(string email, string password)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user is null || !await _users.CheckPasswordAsync(user, password))
            return null;

        var roles = await _users.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_opts.AccessTokenExpiryMinutes),
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpiryMinutes { get; set; } = 60;
}
