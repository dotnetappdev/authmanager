using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Manages registered OAuth2 client applications and issues access tokens for the
/// client-credentials grant — service-to-service auth, the same shape as Keycloak's
/// "Clients" with a confidential client + service account.
/// </summary>
public interface IOAuthClientService
{
    Task<List<OAuthClientDto>> GetClientsAsync(CancellationToken ct = default);
    Task<OAuthClientDto?> GetClientAsync(string id, CancellationToken ct = default);

    /// <summary>Creates a client and returns its secret. The secret is shown once — it is stored hashed.</summary>
    Task<(bool Success, string[] Errors, NewOAuthClientResult? Result)> CreateClientAsync(CreateOAuthClientDto dto, CancellationToken ct = default);

    Task<(bool Success, string[] Errors)> UpdateClientAsync(string id, UpdateOAuthClientDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> DeleteClientAsync(string id, CancellationToken ct = default);

    /// <summary>Issues a new secret for an existing client, invalidating the previous one.</summary>
    Task<(bool Success, string[] Errors, string? NewSecret)> RegenerateSecretAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Validates a client_id/client_secret pair for the client-credentials grant. Returns the
    /// client (and records LastUsedAt) if valid and enabled; null otherwise. Never throws for
    /// bad credentials — that's a normal "invalid_client" outcome, not an exceptional one.
    /// </summary>
    Task<OAuthClientDto?> ValidateClientCredentialsAsync(string clientId, string clientSecret, CancellationToken ct = default);
}
