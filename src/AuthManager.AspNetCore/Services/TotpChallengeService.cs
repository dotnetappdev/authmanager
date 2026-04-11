using AuthManager.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Wraps <see cref="SignInManager{TUser}"/> two-factor methods for use in
/// non-generic Blazor UI components.
/// </summary>
internal sealed class TotpChallengeService<TUser> : ITotpChallengeService
    where TUser : IdentityUser, new()
{
    private readonly SignInManager<TUser> _signIn;

    public TotpChallengeService(SignInManager<TUser> signIn)
        => _signIn = signIn;

    public async Task<TotpResult> VerifyTotpAsync(
        string code, bool isPersistent = false, bool rememberClient = false,
        CancellationToken ct = default)
    {
        var result = await _signIn.TwoFactorAuthenticatorSignInAsync(
            code, isPersistent, rememberClient);

        return result.Succeeded  ? TotpResult.Success  :
               result.IsLockedOut ? TotpResult.LockedOut :
                                    TotpResult.Failed;
    }

    public async Task<TotpResult> VerifyRecoveryCodeAsync(
        string code, CancellationToken ct = default)
    {
        var result = await _signIn.TwoFactorRecoveryCodeSignInAsync(code);

        return result.Succeeded  ? TotpResult.Success  :
               result.IsLockedOut ? TotpResult.LockedOut :
                                    TotpResult.Failed;
    }
}
