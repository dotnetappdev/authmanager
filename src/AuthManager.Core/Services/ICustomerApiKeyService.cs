using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Issues and validates customer-facing API keys — the kind a customer's own application
/// presents on every request, scoped and optionally rate-limited.
/// </summary>
public interface ICustomerApiKeyService
{
    Task<List<CustomerApiKeyDto>> GetKeysAsync(string? customerId = null, CancellationToken ct = default);
    Task<CustomerApiKeyDto?> GetKeyAsync(string id, CancellationToken ct = default);

    /// <summary>Creates a key and returns its raw value. Shown once — stored only as a hash.</summary>
    Task<(bool Success, string[] Errors, NewCustomerApiKeyResult? Result)> CreateKeyAsync(CreateCustomerApiKeyDto dto, CancellationToken ct = default);

    Task<(bool Success, string[] Errors)> UpdateKeyAsync(string id, UpdateCustomerApiKeyDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> RevokeKeyAsync(string id, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> DeleteKeyAsync(string id, CancellationToken ct = default);
    Task<(bool Success, string[] Errors, string? NewKey)> RegenerateKeyAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Validates a raw API key presented by a caller. Returns the key's metadata (and records
    /// LastUsedAt) if valid, enabled, and unexpired; null otherwise. Never throws for a bad key.
    /// </summary>
    Task<CustomerApiKeyDto?> ValidateKeyAsync(string rawKey, CancellationToken ct = default);
}
