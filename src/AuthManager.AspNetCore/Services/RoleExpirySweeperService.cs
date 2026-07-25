using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Periodically revokes role assignments granted via
/// <c>IUserManagementService.AssignTemporaryRoleAsync</c> once they expire.
/// Interval is configurable via <see cref="SecurityPolicyOptions.RoleExpiryCheckInterval"/>.
/// </summary>
internal sealed class RoleExpirySweeperService<TUser> : BackgroundService
    where TUser : IdentityUser, new()
{
    private const string RoleExpiryClaimPrefix = "role_expiry:";

    private readonly IServiceProvider _services;
    private readonly ILogger<RoleExpirySweeperService<TUser>> _logger;

    public RoleExpirySweeperService(IServiceProvider services, ILogger<RoleExpirySweeperService<TUser>> logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = TimeSpan.FromMinutes(5);
            try
            {
                using var scope = _services.CreateScope();
                var opts = scope.ServiceProvider.GetRequiredService<IOptions<AuthManagerOptions>>().Value;
                interval = opts.SecurityPolicy.RoleExpiryCheckInterval;

                await SweepAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Role expiry sweep failed — will retry on the next interval.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SweepAsync(IServiceProvider sp, CancellationToken ct)
    {
        var userManager = sp.GetRequiredService<UserManager<TUser>>();
        var auditService = sp.GetService<IAuditService>();
        var now = DateTimeOffset.UtcNow;

        var users = await userManager.Users.ToListAsync(ct);
        foreach (var user in users)
        {
            var claims = await userManager.GetClaimsAsync(user);
            foreach (var claim in claims.Where(c => c.Type.StartsWith(RoleExpiryClaimPrefix, StringComparison.Ordinal)))
            {
                if (!DateTimeOffset.TryParse(claim.Value, out var expiresAt) || expiresAt > now)
                    continue;

                var roleName = claim.Type[RoleExpiryClaimPrefix.Length..];

                await userManager.RemoveClaimAsync(user, claim);
                var removeResult = await userManager.RemoveFromRoleAsync(user, roleName);

                if (removeResult.Succeeded)
                {
                    _logger.LogInformation(
                        "Temporary role {Role} expired and was revoked for user {UserId}.", roleName, user.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "Temporary role {Role} expired for user {UserId} but revocation failed: {Errors}",
                        roleName, user.Id, string.Join(", ", removeResult.Errors.Select(e => e.Description)));
                }

                if (auditService is not null)
                {
                    await auditService.RecordAsync(new AuditEntry
                    {
                        Action = AuditActions.RoleExpired,
                        EntityType = "User",
                        EntityId = user.Id,
                        EntityName = user.UserName,
                        Success = removeResult.Succeeded,
                        NewValues = new Dictionary<string, object?> { ["role"] = roleName, ["expiresAt"] = expiresAt }
                    }, ct);
                }
            }
        }
    }
}
