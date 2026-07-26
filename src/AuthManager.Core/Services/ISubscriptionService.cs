using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>Manages subscription plans and customers' subscriptions to them.</summary>
public interface ISubscriptionService
{
    // ── Plans ────────────────────────────────────────────────────────────────
    Task<List<SubscriptionPlanDto>> GetPlansAsync(CancellationToken ct = default);
    Task<SubscriptionPlanDto?> GetPlanAsync(string id, CancellationToken ct = default);
    Task<(bool Success, string[] Errors, SubscriptionPlanDto? Plan)> CreatePlanAsync(CreateSubscriptionPlanDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> UpdatePlanAsync(string id, UpdateSubscriptionPlanDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> DeletePlanAsync(string id, CancellationToken ct = default);

    // ── Subscriptions ────────────────────────────────────────────────────────
    Task<List<CustomerSubscriptionDto>> GetSubscriptionsAsync(string? customerId = null, CancellationToken ct = default);
    Task<CustomerSubscriptionDto?> GetSubscriptionAsync(string id, CancellationToken ct = default);
    Task<CustomerSubscriptionDto?> GetActiveSubscriptionForCustomerAsync(string customerId, CancellationToken ct = default);

    Task<(bool Success, string[] Errors, CustomerSubscriptionDto? Subscription)> SubscribeAsync(CreateCustomerSubscriptionDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> ChangePlanAsync(string subscriptionId, ChangeSubscriptionPlanDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> ReactivateSubscriptionAsync(string subscriptionId, CancellationToken ct = default);
}
