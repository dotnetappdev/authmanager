using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

/// <summary>Manages subscription plans and customers' subscriptions to them.</summary>
internal sealed class SubscriptionService : ISubscriptionService
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;

    public SubscriptionService(IDbContextFactory<AuthManagerDbContext> factory) => _factory = factory;

    // ── Plans ────────────────────────────────────────────────────────────────

    public async Task<List<SubscriptionPlanDto>> GetPlansAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var plans = await db.SubscriptionPlans.OrderBy(p => p.PriceCents).ToListAsync(ct);
        return plans.Select(ToPlanDto).ToList();
    }

    public async Task<SubscriptionPlanDto?> GetPlanAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var plan = await db.SubscriptionPlans.FindAsync([id], ct);
        return plan is null ? null : ToPlanDto(plan);
    }

    public async Task<(bool Success, string[] Errors, SubscriptionPlanDto? Plan)> CreatePlanAsync(
        CreateSubscriptionPlanDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, ["Name is required."], null);
        if (dto.PriceCents < 0)
            return (false, ["Price cannot be negative."], null);

        await using var db = await _factory.CreateDbContextAsync(ct);
        if (await db.SubscriptionPlans.AnyAsync(p => p.Name == dto.Name, ct))
            return (false, [$"A plan named '{dto.Name}' already exists."], null);

        var record = new SubscriptionPlanRecord
        {
            Name           = dto.Name.Trim(),
            Description    = dto.Description?.Trim(),
            PriceCents     = dto.PriceCents,
            Currency       = string.IsNullOrWhiteSpace(dto.Currency) ? "USD" : dto.Currency.Trim().ToUpperInvariant(),
            Interval       = dto.Interval.ToString(),
            TrialDays      = dto.TrialDays,
            MaxApiKeys     = dto.MaxApiKeys,
            MaxActivations = dto.MaxActivations,
            FeaturesJson   = JsonSerializer.Serialize(dto.Features ?? [])
        };

        db.SubscriptionPlans.Add(record);
        await db.SaveChangesAsync(ct);
        return (true, [], ToPlanDto(record));
    }

    public async Task<(bool Success, string[] Errors)> UpdatePlanAsync(
        string id, UpdateSubscriptionPlanDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, ["Name is required."]);

        await using var db = await _factory.CreateDbContextAsync(ct);
        var plan = await db.SubscriptionPlans.FindAsync([id], ct);
        if (plan is null) return (false, ["Plan not found."]);

        plan.Name           = dto.Name.Trim();
        plan.Description    = dto.Description?.Trim();
        plan.PriceCents     = dto.PriceCents;
        plan.Currency       = string.IsNullOrWhiteSpace(dto.Currency) ? "USD" : dto.Currency.Trim().ToUpperInvariant();
        plan.Interval       = dto.Interval.ToString();
        plan.TrialDays      = dto.TrialDays;
        plan.MaxApiKeys     = dto.MaxApiKeys;
        plan.MaxActivations = dto.MaxActivations;
        plan.FeaturesJson   = JsonSerializer.Serialize(dto.Features ?? []);
        plan.IsActive       = dto.IsActive;
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> DeletePlanAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var plan = await db.SubscriptionPlans.FindAsync([id], ct);
        if (plan is null) return (false, ["Plan not found."]);
        if (await db.CustomerSubscriptions.AnyAsync(s => s.PlanId == id, ct))
            return (false, ["Cannot delete a plan that has active subscriptions. Cancel or move them first."]);

        db.SubscriptionPlans.Remove(plan);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    // ── Subscriptions ────────────────────────────────────────────────────────

    public async Task<List<CustomerSubscriptionDto>> GetSubscriptionsAsync(string? customerId = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.CustomerSubscriptions.AsQueryable();
        if (!string.IsNullOrEmpty(customerId))
            query = query.Where(s => s.CustomerId == customerId);

        var subs = await query.OrderByDescending(s => s.StartedAt).ToListAsync(ct);
        var result = new List<CustomerSubscriptionDto>(subs.Count);
        foreach (var s in subs)
            result.Add(await ToSubscriptionDtoAsync(db, s, ct));
        return result;
    }

    public async Task<CustomerSubscriptionDto?> GetSubscriptionAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var sub = await db.CustomerSubscriptions.FindAsync([id], ct);
        return sub is null ? null : await ToSubscriptionDtoAsync(db, sub, ct);
    }

    public async Task<CustomerSubscriptionDto?> GetActiveSubscriptionForCustomerAsync(string customerId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var sub = await db.CustomerSubscriptions
            .Where(s => s.CustomerId == customerId && (s.Status == "Active" || s.Status == "Trialing"))
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);
        return sub is null ? null : await ToSubscriptionDtoAsync(db, sub, ct);
    }

    public async Task<(bool Success, string[] Errors, CustomerSubscriptionDto? Subscription)> SubscribeAsync(
        CreateCustomerSubscriptionDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.CustomerId))
            return (false, ["Customer ID is required."], null);
        if (string.IsNullOrWhiteSpace(dto.PlanId))
            return (false, ["Plan ID is required."], null);

        await using var db = await _factory.CreateDbContextAsync(ct);
        if (await db.Customers.FindAsync([dto.CustomerId], ct) is null)
            return (false, ["Customer not found."], null);

        var plan = await db.SubscriptionPlans.FindAsync([dto.PlanId], ct);
        if (plan is null) return (false, ["Plan not found."], null);
        if (!plan.IsActive) return (false, ["This plan is no longer available."], null);

        var now = DateTimeOffset.UtcNow;
        var useTrial = plan.TrialDays > 0 && !dto.SkipTrial;
        var periodLength = plan.Interval switch
        {
            "Yearly"  => TimeSpan.FromDays(365),
            "Weekly"  => TimeSpan.FromDays(7),
            "OneTime" => TimeSpan.FromDays(36500), // effectively never renews
            _         => TimeSpan.FromDays(30)
        };

        var record = new CustomerSubscriptionRecord
        {
            CustomerId       = dto.CustomerId,
            PlanId           = dto.PlanId,
            Status           = useTrial ? "Trialing" : "Active",
            StartedAt        = now,
            TrialEndsAt      = useTrial ? now.AddDays(plan.TrialDays) : null,
            CurrentPeriodEnd = useTrial ? now.AddDays(plan.TrialDays) : now.Add(periodLength)
        };

        db.CustomerSubscriptions.Add(record);
        await db.SaveChangesAsync(ct);
        return (true, [], await ToSubscriptionDtoAsync(db, record, ct));
    }

    public async Task<(bool Success, string[] Errors)> ChangePlanAsync(
        string subscriptionId, ChangeSubscriptionPlanDto dto, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var sub = await db.CustomerSubscriptions.FindAsync([subscriptionId], ct);
        if (sub is null) return (false, ["Subscription not found."]);

        var newPlan = await db.SubscriptionPlans.FindAsync([dto.PlanId], ct);
        if (newPlan is null) return (false, ["Plan not found."]);
        if (!newPlan.IsActive) return (false, ["This plan is no longer available."]);

        sub.PlanId = dto.PlanId;
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var sub = await db.CustomerSubscriptions.FindAsync([subscriptionId], ct);
        if (sub is null) return (false, ["Subscription not found."]);

        sub.Status = "Canceled";
        sub.CanceledAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> ReactivateSubscriptionAsync(string subscriptionId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var sub = await db.CustomerSubscriptions.FindAsync([subscriptionId], ct);
        if (sub is null) return (false, ["Subscription not found."]);
        if (sub.Status != "Canceled") return (false, ["Only a canceled subscription can be reactivated."]);

        sub.Status = "Active";
        sub.CanceledAt = null;
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    private static SubscriptionPlanDto ToPlanDto(SubscriptionPlanRecord p) => new()
    {
        Id             = p.Id,
        Name           = p.Name,
        Description    = p.Description,
        PriceCents     = p.PriceCents,
        Currency       = p.Currency,
        Interval       = Enum.Parse<BillingInterval>(p.Interval),
        TrialDays      = p.TrialDays,
        MaxApiKeys     = p.MaxApiKeys,
        MaxActivations = p.MaxActivations,
        Features       = JsonSerializer.Deserialize<List<string>>(p.FeaturesJson) ?? [],
        IsActive       = p.IsActive
    };

    private static async Task<CustomerSubscriptionDto> ToSubscriptionDtoAsync(
        AuthManagerDbContext db, CustomerSubscriptionRecord s, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([s.CustomerId], ct);
        var plan = await db.SubscriptionPlans.FindAsync([s.PlanId], ct);

        var effectiveStatus = Enum.Parse<SubscriptionStatus>(s.Status);
        if (effectiveStatus is SubscriptionStatus.Active or SubscriptionStatus.Trialing &&
            s.CurrentPeriodEnd < DateTimeOffset.UtcNow)
            effectiveStatus = SubscriptionStatus.Expired;

        return new CustomerSubscriptionDto
        {
            Id               = s.Id,
            CustomerId       = s.CustomerId,
            CustomerName     = customer?.Name,
            PlanId           = s.PlanId,
            PlanName         = plan?.Name,
            Status           = effectiveStatus,
            StartedAt        = s.StartedAt,
            TrialEndsAt      = s.TrialEndsAt,
            CurrentPeriodEnd = s.CurrentPeriodEnd,
            CanceledAt       = s.CanceledAt,
            PaymentProvider  = s.PaymentProvider,
            ExternalSubscriptionId = s.ExternalSubscriptionId
        };
    }
}
