namespace AuthManagerSample.WebApi.Models;

public sealed record RegisterRequest(string Email, string Password, string? DisplayName = null);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType = "Bearer");

public sealed record UserInfoResponse(
    string UserId,
    string Email,
    string UserName,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> RequiredActions);

public sealed record MessageResponse(string Message);
