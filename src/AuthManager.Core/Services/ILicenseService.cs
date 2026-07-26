using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Issues and manages software license keys ("CD keys") — codes a customer enters into a
/// desktop app, installer, or plugin to unlock it, with a maximum number of concurrent
/// machine activations.
/// </summary>
public interface ILicenseService
{
    Task<List<LicenseKeyDto>> GetLicensesAsync(string? customerId = null, CancellationToken ct = default);
    Task<LicenseKeyDto?> GetLicenseAsync(string id, CancellationToken ct = default);

    /// <summary>Generates a new, randomly formatted license key (XXXX-XXXX-XXXX-XXXX).</summary>
    Task<(bool Success, string[] Errors, LicenseKeyDto? License)> CreateLicenseAsync(CreateLicenseKeyDto dto, CancellationToken ct = default);

    Task<(bool Success, string[] Errors)> UpdateLicenseAsync(string id, UpdateLicenseKeyDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> RevokeLicenseAsync(string id, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> DeleteLicenseAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a key is valid (not revoked, not expired) without registering an
    /// activation. Never throws for an unknown/bad key — that's a normal "invalid" result.
    /// </summary>
    Task<LicenseValidationResult> ValidateLicenseAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Validates the key and, if valid, registers this machine as an activation (idempotent —
    /// re-activating the same machine ID doesn't consume another activation slot). Fails once
    /// <see cref="LicenseKeyDto.MaxActivations"/> distinct machines are already active.
    /// </summary>
    Task<(bool Success, string[] Errors, LicenseValidationResult Result)> ActivateLicenseAsync(string key, string machineId, string? ipAddress = null, CancellationToken ct = default);

    /// <summary>Frees up an activation slot for a specific machine, without affecting the key itself.</summary>
    Task<(bool Success, string[] Errors)> DeactivateLicenseAsync(string key, string machineId, CancellationToken ct = default);

    Task<List<LicenseActivationDto>> GetActivationsAsync(string licenseId, CancellationToken ct = default);
}
