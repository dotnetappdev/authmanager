namespace AuthManager.Core.Models;

public sealed class SystemHealthReport
{
    public bool InternalDbHealthy { get; set; }
    public int LockedOutUsers { get; set; }
    public int UnconfirmedEmailUsers { get; set; }
    public int RecentSignInFailures { get; set; } // last hour
    public int ActiveSessions { get; set; }
    public bool JwtConfigured { get; set; }
    public bool OAuthConfigured { get; set; }
    public List<HealthCheckItem> Checks { get; set; } = [];
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    public HealthStatus OverallStatus => Checks.Any(c => c.Status == HealthStatus.Critical)
        ? HealthStatus.Critical
        : Checks.Any(c => c.Status == HealthStatus.Warning)
            ? HealthStatus.Warning
            : HealthStatus.Healthy;
}

public sealed class HealthCheckItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public HealthStatus Status { get; set; }
    public string? Detail { get; set; }
}

public enum HealthStatus { Healthy, Warning, Critical }
