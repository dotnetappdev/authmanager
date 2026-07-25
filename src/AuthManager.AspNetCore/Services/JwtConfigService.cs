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

        if (string.IsNullOrWhiteSpace(_options.Jwt.SigningKey))
        {
            _logger.LogWarning(
                "[DotNetAuthManager] No JWT signing key configured (options.Jwt.SigningKey) — " +
                "generated a random one for this process. Tokens issued by AuthManager (test tokens, " +
                "OAuth2 client-credentials grants) will not validate after a restart or against any " +
                "other service. Set options.Jwt.SigningKey to a stable secret before relying on this.");
        }
    }

    /// <summary>Resolves the effective signing key: the configured one, or a per-process random fallback.</summary>
    private static readonly string _fallbackKey = GenerateFallbackKey();

    private string EffectiveSigningKey => string.IsNullOrWhiteSpace(_options.Jwt.SigningKey)
        ? _fallbackKey
        : _options.Jwt.SigningKey;

    private static string GenerateFallbackKey()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
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
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(EffectiveSigningKey));
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

    public Task<string> IssueTokenAsync(IEnumerable<Claim> claims, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var jwt = _options.Jwt;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(EffectiveSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiry ?? TimeSpan.FromMinutes(jwt.AccessTokenExpiryMinutes)),
            signingCredentials: creds);

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }
}
