using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Issues and validates customer-facing API keys. Keys are generated as cryptographically
/// random strings and stored as SHA-256 hashes — the plaintext is only ever returned once,
/// at creation or regeneration time. Mirrors <see cref="ApiTokenService{TUser}"/>'s approach.
/// </summary>
internal sealed class CustomerApiKeyService : ICustomerApiKeyService
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;

    public CustomerApiKeyService(IDbContextFactory<AuthManagerDbContext> factory) => _factory = factory;

    public async Task<List<CustomerApiKeyDto>> GetKeysAsync(string? customerId = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.CustomerApiKeys.AsQueryable();
        if (!string.IsNullOrEmpty(customerId))
            query = query.Where(k => k.CustomerId == customerId);

        var keys = await query.OrderByDescending(k => k.CreatedAt).ToListAsync(ct);
        var result = new List<CustomerApiKeyDto>(keys.Count);
        foreach (var k in keys)
            result.Add(await ToDtoAsync(db, k, ct));
        return result;
    }

    public async Task<CustomerApiKeyDto?> GetKeyAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var key = await db.CustomerApiKeys.FindAsync([id], ct);
        return key is null ? null : await ToDtoAsync(db, key, ct);
    }

    public async Task<(bool Success, string[] Errors, NewCustomerApiKeyResult? Result)> CreateKeyAsync(
        CreateCustomerApiKeyDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, ["Name is required."], null);
        if (string.IsNullOrWhiteSpace(dto.CustomerId))
            return (false, ["Customer ID is required."], null);

        await using var db = await _factory.CreateDbContextAsync(ct);
        if (await db.Customers.FindAsync([dto.CustomerId], ct) is null)
            return (false, ["Customer not found."], null);

        var (raw, hash, prefix) = GenerateKey();
        var record = new CustomerApiKeyRecord
        {
            CustomerId         = dto.CustomerId,
            Name               = dto.Name.Trim(),
            Prefix             = prefix,
            KeyHash            = hash,
            ScopesJson         = JsonSerializer.Serialize(dto.Scopes ?? []),
            RateLimitPerMinute = dto.RateLimitPerMinute,
            ExpiresAt          = dto.ExpiresAt
        };

        db.CustomerApiKeys.Add(record);
        await db.SaveChangesAsync(ct);

        return (true, [], new NewCustomerApiKeyResult { ApiKey = raw, Key = await ToDtoAsync(db, record, ct) });
    }

    public async Task<(bool Success, string[] Errors)> UpdateKeyAsync(
        string id, UpdateCustomerApiKeyDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, ["Name is required."]);

        await using var db = await _factory.CreateDbContextAsync(ct);
        var key = await db.CustomerApiKeys.FindAsync([id], ct);
        if (key is null) return (false, ["API key not found."]);

        key.Name               = dto.Name.Trim();
        key.ScopesJson         = JsonSerializer.Serialize(dto.Scopes ?? []);
        key.RateLimitPerMinute = dto.RateLimitPerMinute;
        key.Enabled            = dto.Enabled;
        key.ExpiresAt          = dto.ExpiresAt;
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> RevokeKeyAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var key = await db.CustomerApiKeys.FindAsync([id], ct);
        if (key is null) return (false, ["API key not found."]);
        key.Enabled = false;
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> DeleteKeyAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var key = await db.CustomerApiKeys.FindAsync([id], ct);
        if (key is null) return (false, ["API key not found."]);
        db.CustomerApiKeys.Remove(key);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors, string? NewKey)> RegenerateKeyAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var key = await db.CustomerApiKeys.FindAsync([id], ct);
        if (key is null) return (false, ["API key not found."], null);

        var (raw, hash, prefix) = GenerateKey();
        key.KeyHash = hash;
        key.Prefix  = prefix;
        await db.SaveChangesAsync(ct);
        return (true, [], raw);
    }

    public async Task<CustomerApiKeyDto?> ValidateKeyAsync(string rawKey, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rawKey)) return null;

        var hash = HashKey(rawKey);
        await using var db = await _factory.CreateDbContextAsync(ct);
        var key = await db.CustomerApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash, ct);
        if (key is null || !key.Enabled) return null;
        if (key.ExpiresAt.HasValue && key.ExpiresAt < DateTimeOffset.UtcNow) return null;

        key.LastUsedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(db, key, ct);
    }

    private static (string Raw, string Hash, string Prefix) GenerateKey()
    {
        var raw = "ck_live_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLower();
        var prefix = raw[..Math.Min(14, raw.Length)];
        return (raw, HashKey(raw), prefix);
    }

    private static string HashKey(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLower();

    private static async Task<CustomerApiKeyDto> ToDtoAsync(AuthManagerDbContext db, CustomerApiKeyRecord k, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([k.CustomerId], ct);
        return new CustomerApiKeyDto
        {
            Id                 = k.Id,
            CustomerId         = k.CustomerId,
            CustomerName       = customer?.Name,
            Name               = k.Name,
            Prefix             = k.Prefix,
            Scopes             = JsonSerializer.Deserialize<List<string>>(k.ScopesJson) ?? [],
            RateLimitPerMinute = k.RateLimitPerMinute,
            Enabled            = k.Enabled,
            CreatedAt          = k.CreatedAt,
            LastUsedAt         = k.LastUsedAt,
            ExpiresAt          = k.ExpiresAt
        };
    }
}
