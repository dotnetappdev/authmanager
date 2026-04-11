using System.Security.Cryptography;
using System.Text;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Manages long-lived personal API tokens. Tokens are generated as
/// cryptographically random strings and stored as SHA-256 hashes —
/// the plaintext is only returned at creation time.
/// </summary>
internal sealed class ApiTokenService<TUser> : IApiTokenService
    where TUser : IdentityUser, new()
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly UserManager<TUser> _userManager;

    public ApiTokenService(
        IDbContextFactory<AuthManagerDbContext> factory,
        UserManager<TUser> userManager)
    {
        _factory     = factory;
        _userManager = userManager;
    }

    public async Task<List<ApiTokenDto>> GetTokensAsync(
        string? userId = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.ApiTokens.AsQueryable();
        if (!string.IsNullOrEmpty(userId))
            query = query.Where(t => t.UserId == userId);

        var tokens = await query.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
        var result = new List<ApiTokenDto>(tokens.Count);

        foreach (var t in tokens)
        {
            var user = await _userManager.FindByIdAsync(t.UserId);
            result.Add(ToDto(t, user?.UserName));
        }
        return result;
    }

    public async Task<(bool Success, string[] Errors, NewApiTokenResult? Result)> CreateTokenAsync(
        CreateApiTokenDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, ["Token name is required."], null);
        if (string.IsNullOrWhiteSpace(dto.UserId))
            return (false, ["User ID is required."], null);

        // Generate: am_<32 random hex chars>
        var raw    = "am_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLower();
        var hash   = HashToken(raw);
        var prefix = raw[..Math.Min(10, raw.Length)];

        var record = new ApiTokenRecord
        {
            Id        = Guid.NewGuid().ToString("N"),
            Prefix    = prefix,
            TokenHash = hash,
            Name      = dto.Name.Trim(),
            UserId    = dto.UserId,
            Scopes    = dto.Scopes,
            ExpiresAt = dto.ExpiresInDays.HasValue
                            ? DateTimeOffset.UtcNow.AddDays(dto.ExpiresInDays.Value)
                            : null
        };

        await using var db = await _factory.CreateDbContextAsync(ct);
        db.ApiTokens.Add(record);
        await db.SaveChangesAsync(ct);

        var user = await _userManager.FindByIdAsync(dto.UserId);
        return (true, [], new NewApiTokenResult
        {
            RawToken = raw,
            Token    = ToDto(record, user?.UserName)
        });
    }

    public async Task<(bool Success, string[] Errors)> RevokeTokenAsync(
        string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var token = await db.ApiTokens.FindAsync([id], ct);
        if (token is null) return (false, ["Token not found."]);
        token.IsRevoked = true;
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> DeleteTokenAsync(
        string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var token = await db.ApiTokens.FindAsync([id], ct);
        if (token is null) return (false, ["Token not found."]);
        db.ApiTokens.Remove(token);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<ApiTokenDto?> ValidateTokenAsync(
        string rawToken, CancellationToken ct = default)
    {
        var hash = HashToken(rawToken);
        await using var db = await _factory.CreateDbContextAsync(ct);
        var token = await db.ApiTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null || token.IsRevoked) return null;
        if (token.ExpiresAt.HasValue && token.ExpiresAt < DateTimeOffset.UtcNow) return null;

        // Update last-used
        token.LastUsedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(token, null);
    }

    private static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLower();
    }

    private static ApiTokenDto ToDto(ApiTokenRecord t, string? userName) => new()
    {
        Id        = t.Id,
        Prefix    = t.Prefix,
        Name      = t.Name,
        UserId    = t.UserId,
        UserName  = userName,
        Scopes    = t.Scopes,
        IsRevoked = t.IsRevoked,
        CreatedAt  = t.CreatedAt,
        LastUsedAt = t.LastUsedAt,
        ExpiresAt  = t.ExpiresAt
    };
}
