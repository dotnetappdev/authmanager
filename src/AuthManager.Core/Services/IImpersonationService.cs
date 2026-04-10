using Microsoft.AspNetCore.Http;

namespace AuthManager.Core.Services;

public interface IImpersonationService
{
    /// <summary>Creates a one-time token (valid 2 min) for starting impersonation. Returns the token string.</summary>
    Task<string> CreateTokenAsync(string adminUserId, string targetUserId, CancellationToken ct = default);

    /// <summary>Redeems the token: signs the current HTTP context in as the target user with am:impersonating and am:original_admin claims.</summary>
    Task<bool> RedeemTokenAsync(string token, HttpContext ctx, CancellationToken ct = default);

    /// <summary>Signs the HTTP context back in as the original admin user.</summary>
    Task ExitImpersonationAsync(string originalAdminId, HttpContext ctx, CancellationToken ct = default);
}
