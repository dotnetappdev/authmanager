namespace AuthManager.Core.Services;

/// <summary>
/// Wraps ASP.NET Identity's two-factor sign-in methods in a non-generic interface
/// so Blazor UI pages don't need to depend on TUser.
/// </summary>
public interface ITotpChallengeService
{
    /// <summary>
    /// Complete a TOTP two-factor sign-in with an authenticator code.
    /// Returns <c>Success</c>, <c>LockedOut</c>, or <c>Failed</c>.
    /// </summary>
    Task<TotpResult> VerifyTotpAsync(
        string code, bool isPersistent = false, bool rememberClient = false,
        CancellationToken ct = default);

    /// <summary>
    /// Complete a two-factor sign-in using a recovery code.
    /// Returns <c>Success</c>, <c>LockedOut</c>, or <c>Failed</c>.
    /// </summary>
    Task<TotpResult> VerifyRecoveryCodeAsync(string code, CancellationToken ct = default);
}

public enum TotpResult { Success, LockedOut, Failed }
