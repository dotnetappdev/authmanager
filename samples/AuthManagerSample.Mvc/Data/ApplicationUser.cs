using Microsoft.AspNetCore.Identity;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
