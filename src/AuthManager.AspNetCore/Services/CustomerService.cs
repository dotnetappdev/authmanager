using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

/// <summary>Manages customer/account records that licenses, API keys, and subscriptions attach to.</summary>
internal sealed class CustomerService : ICustomerService
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;

    public CustomerService(IDbContextFactory<AuthManagerDbContext> factory) => _factory = factory;

    public async Task<List<CustomerDto>> GetCustomersAsync(string? search = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Name.Contains(term) || c.Email.Contains(term) ||
                                      (c.CompanyName != null && c.CompanyName.Contains(term)));
        }
        var customers = await query.OrderBy(c => c.Name).ToListAsync(ct);
        return customers.Select(ToDto).ToList();
    }

    public async Task<CustomerDto?> GetCustomerAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var customer = await db.Customers.FindAsync([id], ct);
        return customer is null ? null : ToDto(customer);
    }

    public async Task<(bool Success, string[] Errors, CustomerDto? Customer)> CreateCustomerAsync(
        CreateCustomerDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, ["Name is required."], null);
        if (string.IsNullOrWhiteSpace(dto.Email))
            return (false, ["Email is required."], null);

        var record = new CustomerRecord
        {
            Name        = dto.Name.Trim(),
            Email       = dto.Email.Trim(),
            CompanyName = dto.CompanyName?.Trim(),
            Notes       = dto.Notes?.Trim()
        };

        await using var db = await _factory.CreateDbContextAsync(ct);
        db.Customers.Add(record);
        await db.SaveChangesAsync(ct);
        return (true, [], ToDto(record));
    }

    public async Task<(bool Success, string[] Errors)> UpdateCustomerAsync(
        string id, UpdateCustomerDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return (false, ["Name is required."]);
        if (string.IsNullOrWhiteSpace(dto.Email))
            return (false, ["Email is required."]);

        await using var db = await _factory.CreateDbContextAsync(ct);
        var customer = await db.Customers.FindAsync([id], ct);
        if (customer is null) return (false, ["Customer not found."]);

        customer.Name        = dto.Name.Trim();
        customer.Email       = dto.Email.Trim();
        customer.CompanyName = dto.CompanyName?.Trim();
        customer.Notes       = dto.Notes?.Trim();
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> DeleteCustomerAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var customer = await db.Customers.FindAsync([id], ct);
        if (customer is null) return (false, ["Customer not found."]);
        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    private static CustomerDto ToDto(CustomerRecord c) => new()
    {
        Id          = c.Id,
        Name        = c.Name,
        Email       = c.Email,
        CompanyName = c.CompanyName,
        Notes       = c.Notes,
        CreatedAt   = c.CreatedAt
    };
}
