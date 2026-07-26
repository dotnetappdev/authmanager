using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>Manages customer/account records that licenses, API keys, and subscriptions attach to.</summary>
public interface ICustomerService
{
    Task<List<CustomerDto>> GetCustomersAsync(string? search = null, CancellationToken ct = default);
    Task<CustomerDto?> GetCustomerAsync(string id, CancellationToken ct = default);
    Task<(bool Success, string[] Errors, CustomerDto? Customer)> CreateCustomerAsync(CreateCustomerDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> UpdateCustomerAsync(string id, UpdateCustomerDto dto, CancellationToken ct = default);
    Task<(bool Success, string[] Errors)> DeleteCustomerAsync(string id, CancellationToken ct = default);
}
