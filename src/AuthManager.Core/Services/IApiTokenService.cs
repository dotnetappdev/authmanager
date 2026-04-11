using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Manages long-lived personal API tokens (PATs) — similar to GitHub's personal access tokens.
/// Tokens are stored as SHA-256 hashes; the raw value is returned only once on creation.
/// </summary>
public interface IApiTokenService
{
    Task<List<ApiTokenDto>>            GetTokensAsync(string? userId = null, CancellationToken ct = default);
    Task<(bool Success, string[] Errors, NewApiTokenResult? Result)>
                                       CreateTokenAsync(CreateApiTokenDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> RevokeTokenAsync(string id, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> DeleteTokenAsync(string id, CancellationToken ct = default);

    /// <summary>Validates a raw token string. Returns the stored token record if valid.</summary>
    Task<ApiTokenDto?> ValidateTokenAsync(string rawToken, CancellationToken ct = default);
}
