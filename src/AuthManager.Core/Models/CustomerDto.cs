namespace AuthManager.Core.Models;

/// <summary>
/// An external customer/account that licenses, API keys, and subscriptions attach to.
/// Distinct from an Identity user — a customer is a billing/licensing entity, not
/// necessarily someone who ever logs into this app.
/// </summary>
public sealed class CustomerDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? CompanyName { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class CreateCustomerDto
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? CompanyName { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateCustomerDto
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string? CompanyName { get; set; }
    public string? Notes { get; set; }
}
