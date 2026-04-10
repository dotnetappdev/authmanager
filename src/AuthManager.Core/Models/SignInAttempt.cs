namespace AuthManager.Core.Models;

public sealed class SignInAttempt
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool Succeeded { get; set; }
    public string? FailureReason { get; set; } // "InvalidPassword", "LockedOut", "NotAllowed", "UserNotFound"
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
