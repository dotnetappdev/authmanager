namespace AuthManager.Core.Models;

/// <summary>
/// Data transfer object for claims.
/// </summary>
public sealed class ClaimDto
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Issuer { get; set; }
    public string? ValueType { get; set; }

    public ClaimDto() { }

    public ClaimDto(string type, string value)
    {
        Type = type;
        Value = value;
    }
}

/// <summary>
/// Well-known claim types used in the UI.
/// </summary>
public static class WellKnownClaimTypes
{
    public const string Permission = "permission";
    public const string Tenant = "tenant";
    public const string Department = "department";
    public const string EmployeeId = "employee_id";
    public const string Subscription = "subscription";
}
