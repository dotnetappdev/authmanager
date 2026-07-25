using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using AuthManager.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuthManager.Tests.ServiceTests;

/// <summary>
/// Regression coverage: GenerateTestTokenAsync used to sign with a hardcoded placeholder
/// key regardless of configuration, and IssueTokenAsync didn't exist at all — the "JWT
/// Settings" feature was effectively cosmetic. These lock in that both now use the
/// configured (or a real per-process fallback) signing key, and that IssueTokenAsync
/// carries through arbitrary claims for the OAuth2 client-credentials grant.
/// </summary>
public sealed class JwtConfigServiceTests : ServiceTestBase
{
    protected override void ConfigureOptions(AuthManagerOptions options)
    {
        options.Jwt.SigningKey = "unit-test-signing-key-at-least-32-characters-long";
        options.Jwt.Issuer = "https://tests.example.com";
        options.Jwt.Audience = "https://tests.example.com";
    }

    [Fact]
    public async Task GenerateTestTokenAsync_produces_a_token_that_validates_against_the_configured_key()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IJwtConfigService>();

        var token = await svc.GenerateTestTokenAsync("user-123");
        var principal = ValidateAndRead(token, "unit-test-signing-key-at-least-32-characters-long");

        Assert.Equal("user-123", principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value);
    }

    [Fact]
    public async Task GenerateTestTokenAsync_does_not_validate_against_a_different_key()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IJwtConfigService>();

        var token = await svc.GenerateTestTokenAsync("user-123");

        Assert.ThrowsAny<Microsoft.IdentityModel.Tokens.SecurityTokenException>(
            () => ValidateAndRead(token, "a-completely-different-signing-key-value-here!!"));
    }

    [Fact]
    public async Task IssueTokenAsync_carries_through_arbitrary_claims()
    {
        using var scope = CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IJwtConfigService>();

        var token = await svc.IssueTokenAsync(
        [
            new Claim("client_id", "billing-service"),
            new Claim("scope", "read:invoices"),
        ], TimeSpan.FromMinutes(5));

        var principal = ValidateAndRead(token, "unit-test-signing-key-at-least-32-characters-long");
        Assert.Equal("billing-service", principal.FindFirst("client_id")?.Value);
        Assert.Equal("read:invoices", principal.FindFirst("scope")?.Value);
    }

    private static ClaimsPrincipal ValidateAndRead(string token, string signingKey)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.ValidateToken(token, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(signingKey)),
        }, out _);
    }
}
