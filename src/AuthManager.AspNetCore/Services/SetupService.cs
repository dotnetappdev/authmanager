using System.Text.Json;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Handles the one-time first-run setup: creates the SuperAdmin role + user,
/// persists password/security policy, and sets the SetupComplete flag.
/// </summary>
internal sealed class SetupService<TUser, TRole> : ISetupService
    where TUser : IdentityUser, new()
    where TRole : IdentityRole, new()
{
    private const string SetupKey = "SetupComplete";

    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly UserManager<TUser>  _userManager;
    private readonly RoleManager<TRole>  _roleManager;
    private readonly IOptionsMonitor<AuthManagerOptions> _options;

    public SetupService(
        IDbContextFactory<AuthManagerDbContext> factory,
        UserManager<TUser> userManager,
        RoleManager<TRole> roleManager,
        IOptionsMonitor<AuthManagerOptions> options)
    {
        _factory     = factory;
        _userManager = userManager;
        _roleManager = roleManager;
        _options     = options;
    }

    public async Task<bool> IsSetupCompleteAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.Settings.FindAsync([SetupKey], ct);
        return row?.ValueJson == "\"true\"";
    }

    public async Task<(bool Success, string[] Errors)> CompleteSetupAsync(
        SetupWizardDto dto, CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;

        // ── 1. Validate ─────────────────────────────────────────────────────
        var errors = Validate(dto);
        if (errors.Length > 0) return (false, errors);

        // ── 2. Ensure SuperAdmin role ────────────────────────────────────────
        if (!await _roleManager.RoleExistsAsync(opts.SuperAdminRole))
        {
            var role = new TRole();
            await _roleManager.SetRoleNameAsync(role, opts.SuperAdminRole);
            var roleResult = await _roleManager.CreateAsync(role);
            if (!roleResult.Succeeded)
                return (false, roleResult.Errors.Select(e => e.Description).ToArray());
        }

        // ── 3. Create admin user ─────────────────────────────────────────────
        var existing = await _userManager.FindByEmailAsync(dto.AdminEmail);
        if (existing is not null)
            return (false, ["An account with that email address already exists."]);

        var user = new TUser
        {
            UserName       = string.IsNullOrWhiteSpace(dto.AdminUserName)
                                 ? dto.AdminEmail.Split('@')[0]
                                 : dto.AdminUserName.Trim(),
            Email          = dto.AdminEmail.Trim(),
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, dto.AdminPassword);
        if (!createResult.Succeeded)
            return (false, createResult.Errors.Select(e => e.Description).ToArray());

        await _userManager.AddToRoleAsync(user, opts.SuperAdminRole);

        // ── 4. Persist password + security policy ────────────────────────────
        await using var db = await _factory.CreateDbContextAsync(ct);

        var pp = new PasswordPolicyOptions
        {
            MinimumLength          = dto.MinPasswordLength,
            RequireUppercase       = dto.RequireUppercase,
            RequireLowercase       = dto.RequireLowercase,
            RequireDigit           = dto.RequireDigit,
            RequireNonAlphanumeric = dto.RequireSpecialChar
        };
        await UpsertAsync(db, "PasswordPolicy", JsonSerializer.Serialize(pp), ct);

        var sp = new SecurityPolicyOptions
        {
            EnableBruteForceDetection = dto.EnableBruteForce,
            MaxFailedLoginAttempts    = dto.MaxFailedAttempts,
            LockoutDuration           = TimeSpan.FromMinutes(dto.LockoutDurationMinutes)
        };
        await UpsertAsync(db, "SecurityPolicy", JsonSerializer.Serialize(sp), ct);

        // ── 5. Mark setup complete ───────────────────────────────────────────
        await UpsertAsync(db, SetupKey, "\"true\"", ct);
        await db.SaveChangesAsync(ct);

        return (true, []);
    }

    private static string[] Validate(SetupWizardDto dto)
    {
        var errs = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.AdminEmail))
            errs.Add("Email address is required.");
        if (!dto.AdminEmail.Contains('@'))
            errs.Add("Email address is not valid.");
        if (string.IsNullOrWhiteSpace(dto.AdminPassword))
            errs.Add("Password is required.");
        if (dto.AdminPassword != dto.AdminPasswordConfirm)
            errs.Add("Passwords do not match.");
        if (dto.MinPasswordLength < 4)
            errs.Add("Minimum password length must be at least 4.");
        return [.. errs];
    }

    private static async Task UpsertAsync(
        AuthManagerDbContext db, string key, string valueJson, CancellationToken ct)
    {
        var existing = await db.Settings.FindAsync([key], ct);
        if (existing is null)
            db.Settings.Add(new AuthManagerSettingRecord { Key = key, ValueJson = valueJson });
        else
        {
            existing.ValueJson = valueJson;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
