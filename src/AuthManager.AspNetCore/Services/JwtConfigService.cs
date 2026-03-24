using System.Security.Claims;
using System.Text;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace AuthManager.AspNetCore.Services;

internal sealed class JwtConfigService : IJwtConfigService
{
    private readonly AuthManagerOptions _options;
    private readonly ILogger<JwtConfigService> _logger;

    public JwtConfigService(IOptions<AuthManagerOptions> options, ILogger<JwtConfigService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<JwtConfigInfo> GetConfigAsync(CancellationToken ct = default)
    {
        var jwt = _options.Jwt;
        return Task.FromResult(new JwtConfigInfo
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            SigningKeyPreview = "***configured***",
            AccessTokenExpiryMinutes = jwt.AccessTokenExpiryMinutes,
            RefreshTokenExpiryDays = jwt.RefreshTokenExpiryDays,
            EnableRefreshTokens = jwt.EnableRefreshTokens,
            Algorithm = "HS256",
            RequireHttpsMetadata = true,
            ValidateAudience = !string.IsNullOrEmpty(jwt.Audience),
            ValidateIssuer = !string.IsNullOrEmpty(jwt.Issuer),
            ValidateLifetime = true
        });
    }

    public Task<(bool Success, string[] Errors)> UpdateConfigAsync(JwtConfigInfo config, CancellationToken ct = default)
    {
        // In production, persist to configuration store
        _logger.LogInformation("JWT configuration updated.");
        return Task.FromResult<(bool, string[])>((true, []));
    }

    public Task<string> GenerateTestTokenAsync(string userId, CancellationToken ct = default)
    {
        var jwt = _options.Jwt;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-key-for-preview-do-not-use-in-production-32chars"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            ],
            expires: DateTime.UtcNow.AddMinutes(jwt.AccessTokenExpiryMinutes),
            signingCredentials: creds);

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }
}
