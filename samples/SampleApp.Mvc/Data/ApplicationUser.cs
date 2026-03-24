using Microsoft.AspNetCore.Identity;

namespace SampleApp.Mvc.Data;

/// <summary>
/// Extended ApplicationUser — add your custom profile fields here.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
