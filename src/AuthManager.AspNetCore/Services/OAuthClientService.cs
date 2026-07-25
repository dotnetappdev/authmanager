using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Manages registered OAuth2 client applications. Secrets are generated as cryptographically
/// random strings and stored as SHA-256 hashes — the plaintext is only ever returned once,
/// at creation or regeneration time. Mirrors <see cref="ApiTokenService{TUser}"/>'s approach.
/// </summary>
internal sealed class OAuthClientService : IOAuthClientService
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;

    public OAuthClientService(IDbContextFactory<AuthManagerDbContext> factory)
        => _factory = factory;

    public async Task<List<OAuthClientDto>> GetClientsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var clients = await db.OAuthClients.OrderBy(c => c.Name).ToListAsync(ct);
        return clients.Select(ToDto).ToList();
    }

    public async Task<OAuthClientDto?> GetClientAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var client = await db.OAuthClients.FindAsync([id], ct);
        return client is null ? null : ToDto(client);
    }

    public async Task<(bool Success, string[] Errors, NewOAuthClientResult? Result)> CreateClientAsync(
        CreateOAuthClientDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.ClientId))
            return (false, ["Client ID is required."], null);
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, ["Name is required."], null);

        await using var db = await _factory.CreateDbContextAsync(ct);
        if (await db.OAuthClients.AnyAsync(c => c.ClientId == dto.ClientId, ct))
            return (false, [$"A client with ID '{dto.ClientId}' already exists."], null);

        var (secret, hash) = GenerateSecret();
        var record = new OAuthClientRecord
        {
            ClientId    = dto.ClientId.Trim(),
            SecretHash  = hash,
            Name        = dto.Name.Trim(),
            Description = dto.Description?.Trim(),
            ScopesJson  = JsonSerializer.Serialize(dto.AllowedScopes ?? [])
        };

        db.OAuthClients.Add(record);
        await db.SaveChangesAsync(ct);

        return (true, [], new NewOAuthClientResult { ClientSecret = secret, Client = ToDto(record) });
    }

    public async Task<(bool Success, string[] Errors)> UpdateClientAsync(
        string id, UpdateOAuthClientDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, ["Name is required."]);

        await using var db = await _factory.CreateDbContextAsync(ct);
        var client = await db.OAuthClients.FindAsync([id], ct);
        if (client is null) return (false, ["Client not found."]);

        client.Name        = dto.Name.Trim();
        client.Description = dto.Description?.Trim();
        client.Enabled      = dto.Enabled;
        client.ScopesJson  = JsonSerializer.Serialize(dto.AllowedScopes ?? []);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> DeleteClientAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var client = await db.OAuthClients.FindAsync([id], ct);
        if (client is null) return (false, ["Client not found."]);
        db.OAuthClients.Remove(client);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors, string? NewSecret)> RegenerateSecretAsync(
        string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var client = await db.OAuthClients.FindAsync([id], ct);
        if (client is null) return (false, ["Client not found."], null);

        var (secret, hash) = GenerateSecret();
        client.SecretHash = hash;
        await db.SaveChangesAsync(ct);
        return (true, [], secret);
    }

    public async Task<OAuthClientDto?> ValidateClientCredentialsAsync(
        string clientId, string clientSecret, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return null;

        await using var db = await _factory.CreateDbContextAsync(ct);
        var client = await db.OAuthClients.FirstOrDefaultAsync(c => c.ClientId == clientId, ct);
        if (client is null || !client.Enabled) return null;
        if (client.SecretHash != HashSecret(clientSecret)) return null;

        client.LastUsedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(client);
    }

    private static (string Secret, string Hash) GenerateSecret()
    {
        var secret = "cs_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();
        return (secret, HashSecret(secret));
    }

    private static string HashSecret(string raw)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLower();

    private static OAuthClientDto ToDto(OAuthClientRecord c) => new()
    {
        Id           = c.Id,
        ClientId     = c.ClientId,
        Name         = c.Name,
        Description  = c.Description,
        Enabled      = c.Enabled,
        AllowedScopes = JsonSerializer.Deserialize<List<string>>(c.ScopesJson) ?? [],
        CreatedAt    = c.CreatedAt,
        LastUsedAt   = c.LastUsedAt
    };
}
