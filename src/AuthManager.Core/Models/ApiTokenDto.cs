namespace AuthManager.Core.Models;

public sealed class ApiTokenDto
{
    public string  Id         { get; set; } = "";
    public string  Prefix     { get; set; } = "";   // e.g. "am_abc123" — shown in list
    public string  Name       { get; set; } = "";
    public string  UserId     { get; set; } = "";
    public string? UserName   { get; set; }
    public string? Scopes     { get; set; }
    public bool    IsRevoked  { get; set; }
    public DateTimeOffset  CreatedAt  { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt  { get; set; }
}

public sealed class CreateApiTokenDto
{
    public string  Name     { get; set; } = "";
    public string  UserId   { get; set; } = "";
    public string? Scopes   { get; set; }
    public int?    ExpiresInDays { get; set; }
}

/// <summary>Returned once on creation — caller must store the raw token securely.</summary>
public sealed class NewApiTokenResult
{
    public string      RawToken { get; set; } = "";   // shown once
    public ApiTokenDto Token    { get; set; } = new();
}
