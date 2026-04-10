using Microsoft.AspNetCore.Identity;

namespace AuthManagerSample.WebApi.Identity;

/// <summary>
/// Application user extending ASP.NET Identity's <see cref="IdentityUser"/>.
/// Add custom properties here to persist them to the Identity database.
/// Dynamic runtime fields (added via the AuthManager UI) are stored as
/// <c>custom:fieldname</c> claims and do not require schema changes.
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>User's given name.</summary>
    public string? FirstName { get; set; }

    /// <summary>User's family name.</summary>
    public string? LastName { get; set; }

    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Optional profile picture URL.</summary>
    public string? ProfilePictureUrl { get; set; }

    /// <summary>Convenience property for display purposes.</summary>
    public string DisplayName =>
        (!string.IsNullOrWhiteSpace(FirstName) || !string.IsNullOrWhiteSpace(LastName))
            ? $"{FirstName} {LastName}".Trim()
            : UserName ?? Email ?? string.Empty;
}
