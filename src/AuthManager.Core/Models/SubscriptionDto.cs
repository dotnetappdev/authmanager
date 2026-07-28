namespace AuthManager.Core.Models;

/// <summary>Billing cadence for a subscription plan.</summary>
public enum BillingInterval
{
    Monthly,
    Yearly,
    Weekly,
    OneTime
}

/// <summary>Lifecycle state of a customer's subscription.</summary>
public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    Canceled,
    Expired
}

/// <summary>A plan customers can subscribe to (e.g. "Free", "Pro", "Enterprise").</summary>
public sealed class SubscriptionPlanDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int PriceCents { get; set; }
    public string Currency { get; set; } = "USD";
    public BillingInterval Interval { get; set; } = BillingInterval.Monthly;
    public int TrialDays { get; set; }
    public int? MaxApiKeys { get; set; }
    public int? MaxActivations { get; set; }
    public List<string> Features { get; set; } = [];
    public bool IsActive { get; set; } = true;
}

public sealed class CreateSubscriptionPlanDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int PriceCents { get; set; }
    public string Currency { get; set; } = "USD";
    public BillingInterval Interval { get; set; } = BillingInterval.Monthly;
    public int TrialDays { get; set; }
    public int? MaxApiKeys { get; set; }
    public int? MaxActivations { get; set; }
    public List<string> Features { get; set; } = [];
}

public sealed class UpdateSubscriptionPlanDto
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int PriceCents { get; set; }
    public string Currency { get; set; } = "USD";
    public BillingInterval Interval { get; set; } = BillingInterval.Monthly;
    public int TrialDays { get; set; }
    public int? MaxApiKeys { get; set; }
    public int? MaxActivations { get; set; }
    public List<string> Features { get; set; } = [];
    public bool IsActive { get; set; } = true;
}

/// <summary>A customer's subscription to a plan.</summary>
public sealed class CustomerSubscriptionDto
{
    public string Id { get; set; } = "";
    public string CustomerId { get; set; } = "";
    public string? CustomerName { get; set; }
    public string PlanId { get; set; } = "";
    public string? PlanName { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? TrialEndsAt { get; set; }
    public DateTimeOffset CurrentPeriodEnd { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }

    /// <summary>"None" (internal/manual billing) | "Stripe" | "PayPal".</summary>
    public string PaymentProvider { get; set; } = "None";

    /// <summary>Stripe Subscription ID or PayPal Order/Subscription ID, when billed through a payment provider.</summary>
    public string? ExternalSubscriptionId { get; set; }
}

public sealed class CreateCustomerSubscriptionDto
{
    public string CustomerId { get; set; } = "";
    public string PlanId { get; set; } = "";

    /// <summary>Skip the plan's trial period and start active immediately.</summary>
    public bool SkipTrial { get; set; }
}

/// <summary>Move a customer to a different plan, effective immediately.</summary>
public sealed class ChangeSubscriptionPlanDto
{
    public string PlanId { get; set; } = "";
}
