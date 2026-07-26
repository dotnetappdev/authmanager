using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Wraps ASP.NET Core Identity's native passkey (WebAuthn) support — <c>SignInManager</c>'s
/// creation/attestation/request/assertion ceremony methods and <c>UserManager</c>'s passkey
/// store — in a non-generic interface, mirroring <see cref="ITotpChallengeService"/>. Lets a
/// user register a device passkey (fingerprint, face, security key) as an alternative to a
/// password, and sign in with it.
/// </summary>
public interface IPasskeyService
{
    /// <summary>Whether the configured Identity store supports passkeys (it does, out of the box, on .NET 9+).</summary>
    bool SupportsPasskeys { get; }

    /// <summary>Creation options JSON to pass to <c>navigator.credentials.create()</c>, scoped to this user.</summary>
    Task<string?> GetCreationOptionsAsync(string userId, CancellationToken ct = default);

    /// <summary>Verifies the attestation response from the browser and stores the new passkey against the user.</summary>
    Task<(bool Success, string? Error)> CompleteRegistrationAsync(string userId, string attestationResponseJson, CancellationToken ct = default);

    Task<List<PasskeyInfoDto>> GetPasskeysAsync(string userId, CancellationToken ct = default);

    Task<(bool Success, string[] Errors)> RemovePasskeyAsync(string userId, string credentialId, CancellationToken ct = default);

    /// <summary>
    /// Request options JSON for <c>navigator.credentials.get()</c>. Pass a username/email to scope the
    /// ceremony to one account, or omit it for a fully passwordless/discoverable-credential flow.
    /// </summary>
    Task<string> GetRequestOptionsAsync(string? userName = null, CancellationToken ct = default);

    /// <summary>Completes a passkey sign-in ceremony, issuing the auth cookie on success.</summary>
    Task<PasskeySignInOutcome> SignInAsync(string assertionResponseJson, CancellationToken ct = default);
}
