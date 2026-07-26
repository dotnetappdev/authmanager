namespace AuthManager.Core.Models;

/// <summary>Lifecycle state of a license key.</summary>
public enum LicenseStatus
{
    Active,
    Revoked,
    Expired
}

/// <summary>
/// A software license / product key ("CD key") — a code a customer enters into a desktop app,
/// installer, or plugin to unlock it. Supports a maximum number of concurrent activations
/// (e.g. "up to 3 machines"), each tracked individually so a specific machine can be deactivated
/// without revoking the whole key.
/// </summary>
public sealed class LicenseKeyDto
{
    public string Id { get; set; } = "";

    /// <summary>The code itself, formatted like <c>XXXX-XXXX-XXXX-XXXX</c>.</summary>
    public string Key { get; set; } = "";

    public string ProductName { get; set; } = "";
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public int MaxActivations { get; set; } = 1;
    public int ActivationCount { get; set; }
    public LicenseStatus Status { get; set; } = LicenseStatus.Active;
    public string? Notes { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class CreateLicenseKeyDto
{
    public string ProductName { get; set; } = "";
    public string? CustomerId { get; set; }
    public int MaxActivations { get; set; } = 1;
    public string? Notes { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

public sealed class UpdateLicenseKeyDto
{
    public string ProductName { get; set; } = "";
    public string? CustomerId { get; set; }
    public int MaxActivations { get; set; } = 1;
    public string? Notes { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>A single machine/device activation against a license key.</summary>
public sealed class LicenseActivationDto
{
    public string Id { get; set; } = "";
    public string LicenseKeyId { get; set; } = "";
    public string MachineId { get; set; } = "";
    public string? IpAddress { get; set; }
    public DateTimeOffset ActivatedAt { get; set; }
}

/// <summary>Result of validating a license key against the outside world (no auth required).</summary>
public sealed class LicenseValidationResult
{
    public bool Valid { get; set; }
    public string? Reason { get; set; }
    public string? ProductName { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
