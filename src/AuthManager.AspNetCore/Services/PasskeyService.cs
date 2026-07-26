using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Wraps <see cref="SignInManager{TUser}"/>'s passkey ceremony methods and
/// <see cref="UserManager{TUser}"/>'s passkey store for use in non-generic Blazor UI
/// components and minimal API endpoints, mirroring <see cref="TotpChallengeService{TUser}"/>.
/// </summary>
internal sealed class PasskeyService<TUser> : IPasskeyService
    where TUser : IdentityUser, new()
{
    private readonly SignInManager<TUser> _signIn;
    private readonly UserManager<TUser> _users;

    public PasskeyService(SignInManager<TUser> signIn, UserManager<TUser> users)
    {
        _signIn = signIn;
        _users  = users;
    }

    public bool SupportsPasskeys => _users.SupportsUserPasskey;

    public async Task<string?> GetCreationOptionsAsync(string userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return null;

        var userName = await _users.GetUserNameAsync(user) ?? userId;
        var entity = new PasskeyUserEntity { Id = userId, Name = userName, DisplayName = userName };
        return await _signIn.MakePasskeyCreationOptionsAsync(entity);
    }

    public async Task<(bool Success, string? Error)> CompleteRegistrationAsync(
        string userId, string attestationResponseJson, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return (false, "User not found.");

        var attestation = await _signIn.PerformPasskeyAttestationAsync(attestationResponseJson);
        if (!attestation.Succeeded || attestation.Passkey is null)
            return (false, attestation.Failure?.Message ?? "Passkey registration failed.");

        var identityResult = await _users.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
        return identityResult.Succeeded
            ? (true, null)
            : (false, string.Join(" ", identityResult.Errors.Select(e => e.Description)));
    }

    public async Task<List<PasskeyInfoDto>> GetPasskeysAsync(string userId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return [];

        var passkeys = await _users.GetPasskeysAsync(user);
        return passkeys.Select(p => new PasskeyInfoDto
        {
            CredentialId = Convert.ToBase64String(p.CredentialId),
            Name         = string.IsNullOrEmpty(p.Name) ? "Passkey" : p.Name,
            CreatedAt    = p.CreatedAt,
            IsBackedUp   = p.IsBackedUp
        }).ToList();
    }

    public async Task<(bool Success, string[] Errors)> RemovePasskeyAsync(
        string userId, string credentialId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user is null) return (false, ["User not found."]);

        byte[] rawId;
        try { rawId = Convert.FromBase64String(credentialId); }
        catch (FormatException) { return (false, ["Invalid credential ID."]); }

        var result = await _users.RemovePasskeyAsync(user, rawId);
        return result.Succeeded
            ? (true, [])
            : (false, result.Errors.Select(e => e.Description).ToArray());
    }

    public async Task<string> GetRequestOptionsAsync(string? userName = null, CancellationToken ct = default)
    {
        TUser? user = null;
        if (!string.IsNullOrWhiteSpace(userName))
            user = await _users.FindByNameAsync(userName) ?? await _users.FindByEmailAsync(userName);

        // A null user is valid — it produces discoverable-credential ("usernameless") request
        // options, letting the browser's platform authenticator prompt for any passkey it has
        // stored for this site rather than one tied to a specific account.
        return await _signIn.MakePasskeyRequestOptionsAsync(user!);
    }

    public async Task<PasskeySignInOutcome> SignInAsync(string assertionResponseJson, CancellationToken ct = default)
    {
        SignInResult result;
        try
        {
            // A caller-supplied credential blob that's malformed JSON, or otherwise doesn't
            // parse as a WebAuthn assertion, throws (PasskeyException, JsonException, or a
            // FormatException from bad base64url) rather than returning a "Failed" SignInResult.
            // This is an anonymous endpoint fed arbitrary input by design (that's how a caller
            // authenticates), so any parse failure must map to a normal "sign-in failed" outcome,
            // never a 500.
            result = await _signIn.PasskeySignInAsync(assertionResponseJson);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException or StackOverflowException))
        {
            return PasskeySignInOutcome.Failed;
        }

        if (result.Succeeded) return PasskeySignInOutcome.Succeeded;
        if (result.RequiresTwoFactor) return PasskeySignInOutcome.RequiresTwoFactor;
        if (result.IsLockedOut) return PasskeySignInOutcome.LockedOut;
        if (result.IsNotAllowed) return PasskeySignInOutcome.NotAllowed;
        return PasskeySignInOutcome.Failed;
    }
}
