using System.Security.Claims;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Scoped impersonation service — allows a SuperAdmin to temporarily sign in as
/// another user via a short-lived one-time token.
/// </summary>
internal sealed class ImpersonationService<TUser> : IImpersonationService
    where TUser : IdentityUser
{
    private readonly UserManager<TUser>                      _userManager;
    private readonly SignInManager<TUser>                    _signInManager;
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;

    public ImpersonationService(
        UserManager<TUser>                      userManager,
        SignInManager<TUser>                    signInManager,
        IDbContextFactory<AuthManagerDbContext> factory)
    {
        _userManager   = userManager;
        _signInManager = signInManager;
        _factory       = factory;
    }

    public async Task<string> CreateTokenAsync(
        string adminUserId,
        string targetUserId,
        CancellationToken ct = default)
    {
        // 32 random bytes → base64url string (no padding, URL-safe)
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        await using var db = await _factory.CreateDbContextAsync(ct);
        db.ImpersonationTokens.Add(new ImpersonationTokenRecord
        {
            Token        = token,
            AdminUserId  = adminUserId,
            TargetUserId = targetUserId,
            ExpiresAt    = DateTimeOffset.UtcNow.AddMinutes(2),
        });
        await db.SaveChangesAsync(ct);

        return token;
    }

    public async Task<bool> RedeemTokenAsync(
        string      token,
        HttpContext ctx,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var record = await db.ImpersonationTokens.FindAsync([token], ct);

        if (record is null || record.ExpiresAt < DateTimeOffset.UtcNow)
            return false;

        var targetUser = await _userManager.FindByIdAsync(record.TargetUserId);
        if (targetUser is null)
            return false;

        var adminUserId = record.AdminUserId;

        // Consume the token immediately
        db.ImpersonationTokens.Remove(record);
        await db.SaveChangesAsync(ct);

        // Sign in as the target user with impersonation claims
        await _signInManager.SignInWithClaimsAsync(targetUser, isPersistent: false, new[]
        {
            new Claim("am:impersonating",  "true"),
            new Claim("am:original_admin", adminUserId),
        });

        return true;
    }

    public async Task ExitImpersonationAsync(
        string      originalAdminId,
        HttpContext ctx,
        CancellationToken ct = default)
    {
        var adminUser = await _userManager.FindByIdAsync(originalAdminId);
        if (adminUser is null)
            return;

        await _signInManager.SignInAsync(adminUser, isPersistent: false);
    }
}
