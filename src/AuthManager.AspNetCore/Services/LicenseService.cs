using System.Security.Cryptography;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Issues and manages software license keys ("CD keys"). Unlike API tokens/OAuth secrets,
/// license keys are stored as plain text — they must be human-readable and re-displayable
/// (an admin resending a lost key, an installer prompting for it again) — so there's no hash
/// to protect against a database compromise the way a bearer credential would need.
/// </summary>
internal sealed class LicenseService : ILicenseService
{
    // Crockford-ish alphabet: no 0/O or 1/I, so a customer reading a key aloud or typing it
    // from a printed card can't confuse similar-looking characters.
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly IDbContextFactory<AuthManagerDbContext> _factory;

    public LicenseService(IDbContextFactory<AuthManagerDbContext> factory) => _factory = factory;

    public async Task<List<LicenseKeyDto>> GetLicensesAsync(string? customerId = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var query = db.LicenseKeys.AsQueryable();
        if (!string.IsNullOrEmpty(customerId))
            query = query.Where(l => l.CustomerId == customerId);

        var licenses = await query.OrderByDescending(l => l.IssuedAt).ToListAsync(ct);
        var result = new List<LicenseKeyDto>(licenses.Count);
        foreach (var l in licenses)
            result.Add(await ToDtoAsync(db, l, ct));
        return result;
    }

    public async Task<LicenseKeyDto?> GetLicenseAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var license = await db.LicenseKeys.FindAsync([id], ct);
        return license is null ? null : await ToDtoAsync(db, license, ct);
    }

    public async Task<(bool Success, string[] Errors, LicenseKeyDto? License)> CreateLicenseAsync(
        CreateLicenseKeyDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.ProductName))
            return (false, ["Product name is required."], null);
        if (dto.MaxActivations < 1)
            return (false, ["Max activations must be at least 1."], null);

        await using var db = await _factory.CreateDbContextAsync(ct);

        if (!string.IsNullOrEmpty(dto.CustomerId) && await db.Customers.FindAsync([dto.CustomerId], ct) is null)
            return (false, ["Customer not found."], null);

        string key;
        do { key = GenerateKey(); }
        while (await db.LicenseKeys.AnyAsync(l => l.Key == key, ct));

        var record = new LicenseKeyRecord
        {
            Key            = key,
            ProductName    = dto.ProductName.Trim(),
            CustomerId     = dto.CustomerId,
            MaxActivations = dto.MaxActivations,
            Notes          = dto.Notes?.Trim(),
            ExpiresAt      = dto.ExpiresAt
        };

        db.LicenseKeys.Add(record);
        await db.SaveChangesAsync(ct);
        return (true, [], await ToDtoAsync(db, record, ct));
    }

    public async Task<(bool Success, string[] Errors)> UpdateLicenseAsync(
        string id, UpdateLicenseKeyDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.ProductName))
            return (false, ["Product name is required."]);
        if (dto.MaxActivations < 1)
            return (false, ["Max activations must be at least 1."]);

        await using var db = await _factory.CreateDbContextAsync(ct);
        var license = await db.LicenseKeys.FindAsync([id], ct);
        if (license is null) return (false, ["License not found."]);

        license.ProductName    = dto.ProductName.Trim();
        license.CustomerId     = dto.CustomerId;
        license.MaxActivations = dto.MaxActivations;
        license.Notes          = dto.Notes?.Trim();
        license.ExpiresAt      = dto.ExpiresAt;
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> RevokeLicenseAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var license = await db.LicenseKeys.FindAsync([id], ct);
        if (license is null) return (false, ["License not found."]);
        license.Status = "Revoked";
        license.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<(bool Success, string[] Errors)> DeleteLicenseAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var license = await db.LicenseKeys.FindAsync([id], ct);
        if (license is null) return (false, ["License not found."]);

        var activations = db.LicenseActivations.Where(a => a.LicenseKeyId == id);
        db.LicenseActivations.RemoveRange(activations);
        db.LicenseKeys.Remove(license);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<LicenseValidationResult> ValidateLicenseAsync(string key, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var license = await db.LicenseKeys.FirstOrDefaultAsync(l => l.Key == key, ct);
        return Evaluate(license);
    }

    public async Task<(bool Success, string[] Errors, LicenseValidationResult Result)> ActivateLicenseAsync(
        string key, string machineId, string? ipAddress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(machineId))
            return (false, ["Machine ID is required."], new LicenseValidationResult { Valid = false, Reason = "Machine ID is required." });

        await using var db = await _factory.CreateDbContextAsync(ct);
        var license = await db.LicenseKeys.FirstOrDefaultAsync(l => l.Key == key, ct);
        var validation = Evaluate(license);
        if (!validation.Valid)
            return (false, [validation.Reason ?? "License is not valid."], validation);

        var existing = await db.LicenseActivations
            .FirstOrDefaultAsync(a => a.LicenseKeyId == license!.Id && a.MachineId == machineId, ct);
        if (existing is not null)
            return (true, [], validation); // idempotent re-activation of the same machine

        var activeCount = await db.LicenseActivations.CountAsync(a => a.LicenseKeyId == license!.Id, ct);
        if (activeCount >= license!.MaxActivations)
        {
            var maxedOut = new LicenseValidationResult { Valid = false, Reason = "Maximum number of activations reached for this license." };
            return (false, [maxedOut.Reason!], maxedOut);
        }

        db.LicenseActivations.Add(new LicenseActivationRecord
        {
            LicenseKeyId = license.Id,
            MachineId    = machineId,
            IpAddress    = ipAddress
        });
        await db.SaveChangesAsync(ct);
        return (true, [], validation);
    }

    public async Task<(bool Success, string[] Errors)> DeactivateLicenseAsync(
        string key, string machineId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var license = await db.LicenseKeys.FirstOrDefaultAsync(l => l.Key == key, ct);
        if (license is null) return (false, ["License not found."]);

        var activation = await db.LicenseActivations
            .FirstOrDefaultAsync(a => a.LicenseKeyId == license.Id && a.MachineId == machineId, ct);
        if (activation is null) return (false, ["This machine is not activated against the license."]);

        db.LicenseActivations.Remove(activation);
        await db.SaveChangesAsync(ct);
        return (true, []);
    }

    public async Task<List<LicenseActivationDto>> GetActivationsAsync(string licenseId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var activations = await db.LicenseActivations
            .Where(a => a.LicenseKeyId == licenseId)
            .OrderByDescending(a => a.ActivatedAt)
            .ToListAsync(ct);

        return activations.Select(a => new LicenseActivationDto
        {
            Id           = a.Id,
            LicenseKeyId = a.LicenseKeyId,
            MachineId    = a.MachineId,
            IpAddress    = a.IpAddress,
            ActivatedAt  = a.ActivatedAt
        }).ToList();
    }

    private static LicenseValidationResult Evaluate(LicenseKeyRecord? license)
    {
        if (license is null)
            return new LicenseValidationResult { Valid = false, Reason = "License key not found." };
        if (license.Status == "Revoked")
            return new LicenseValidationResult { Valid = false, Reason = "License key has been revoked." };
        if (license.ExpiresAt.HasValue && license.ExpiresAt.Value < DateTimeOffset.UtcNow)
            return new LicenseValidationResult { Valid = false, Reason = "License key has expired.", ExpiresAt = license.ExpiresAt };

        return new LicenseValidationResult
        {
            Valid       = true,
            ProductName = license.ProductName,
            ExpiresAt   = license.ExpiresAt
        };
    }

    private static string GenerateKey()
    {
        var groups = new string[4];
        for (var g = 0; g < 4; g++)
        {
            var chars = new char[4];
            for (var i = 0; i < 4; i++)
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            groups[g] = new string(chars);
        }
        return string.Join('-', groups);
    }

    private static async Task<LicenseKeyDto> ToDtoAsync(AuthManagerDbContext db, LicenseKeyRecord l, CancellationToken ct)
    {
        var activationCount = await db.LicenseActivations.CountAsync(a => a.LicenseKeyId == l.Id, ct);
        var customerName = l.CustomerId is null
            ? null
            : (await db.Customers.FindAsync([l.CustomerId], ct))?.Name;

        var effectiveStatus = l.Status == "Revoked" ? LicenseStatus.Revoked
                             : l.ExpiresAt.HasValue && l.ExpiresAt.Value < DateTimeOffset.UtcNow ? LicenseStatus.Expired
                             : LicenseStatus.Active;

        return new LicenseKeyDto
        {
            Id              = l.Id,
            Key             = l.Key,
            ProductName     = l.ProductName,
            CustomerId      = l.CustomerId,
            CustomerName    = customerName,
            MaxActivations  = l.MaxActivations,
            ActivationCount = activationCount,
            Status          = effectiveStatus,
            Notes           = l.Notes,
            IssuedAt        = l.IssuedAt,
            ExpiresAt       = l.ExpiresAt,
            RevokedAt       = l.RevokedAt
        };
    }
}
