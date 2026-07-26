namespace AuthManager.Core.Models;

/// <summary>A passkey (WebAuthn credential) registered against a user.</summary>
public sealed class PasskeyInfoDto
{
    /// <summary>Base64-encoded credential ID — pass this back to remove the passkey.</summary>
    public string CredentialId { get; set; } = "";

    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsBackedUp { get; set; }
}

/// <summary>Outcome of a passkey sign-in ceremony — mirrors ASP.NET Identity's <c>SignInResult</c>.</summary>
public enum PasskeySignInOutcome
{
    Succeeded,
    RequiresTwoFactor,
    LockedOut,
    NotAllowed,
    Failed
}
