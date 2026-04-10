using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// Scoped health service — builds a <see cref="SystemHealthReport"/> by querying
/// the internal DB, sign-in history, sessions, and auth configuration.
/// Scoped so it can use the scoped IUserManagementService.
/// </summary>
internal sealed class SystemHealthService : ISystemHealthService
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly ISignInHistoryService                   _signInHistory;
    private readonly ISessionService                         _sessions;
    private readonly IOptions<AuthManagerOptions>            _options;
    private readonly IUserManagementService                  _userManagement;

    public SystemHealthService(
        IDbContextFactory<AuthManagerDbContext> factory,
        ISignInHistoryService                   signInHistory,
        ISessionService                         sessions,
        IOptions<AuthManagerOptions>            options,
        IUserManagementService                  userManagement)
    {
        _factory        = factory;
        _signInHistory  = signInHistory;
        _sessions       = sessions;
        _options        = options;
        _userManagement = userManagement;
    }

    public async Task<SystemHealthReport> GetHealthAsync(CancellationToken ct = default)
    {
        var report = new SystemHealthReport();
        var opts   = _options.Value;

        // ── 1. Internal DB connectivity ──────────────────────────────────────
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            await db.Database.CanConnectAsync(ct);
            report.InternalDbHealthy = true;
            report.Checks.Add(new HealthCheckItem
            {
                Name        = "Internal Database",
                Description = "AuthManager's SQLite/SQL Server data store",
                Status      = HealthStatus.Healthy,
                Detail      = "Connection successful",
            });
        }
        catch (Exception ex)
        {
            report.InternalDbHealthy = false;
            report.Checks.Add(new HealthCheckItem
            {
                Name        = "Internal Database",
                Description = "AuthManager's SQLite/SQL Server data store",
                Status      = HealthStatus.Critical,
                Detail      = ex.Message,
            });
        }

        // ── 2. User stats ────────────────────────────────────────────────────
        try
        {
            var stats = await _userManagement.GetDashboardStatsAsync(ct);
            report.LockedOutUsers        = stats.LockedOutUsers;
            report.UnconfirmedEmailUsers = stats.UnconfirmedEmailUsers;

            report.Checks.Add(new HealthCheckItem
            {
                Name        = "Locked-out Users",
                Description = "Users currently locked out of their accounts",
                Status      = stats.LockedOutUsers > 10 ? HealthStatus.Warning : HealthStatus.Healthy,
                Detail      = $"{stats.LockedOutUsers} user(s) locked out",
            });

            report.Checks.Add(new HealthCheckItem
            {
                Name        = "Unconfirmed Emails",
                Description = "Users with unverified email addresses",
                Status      = stats.UnconfirmedEmailUsers > 50 ? HealthStatus.Warning : HealthStatus.Healthy,
                Detail      = $"{stats.UnconfirmedEmailUsers} user(s) with unconfirmed email",
            });
        }
        catch (Exception ex)
        {
            report.Checks.Add(new HealthCheckItem
            {
                Name        = "User Statistics",
                Description = "Could not retrieve user statistics",
                Status      = HealthStatus.Warning,
                Detail      = ex.Message,
            });
        }

        // ── 3. Recent sign-in failures (last hour) ───────────────────────────
        try
        {
            var failures = await _signInHistory.GetTotalFailuresAsync(TimeSpan.FromHours(1), ct);
            report.RecentSignInFailures = failures;
            report.Checks.Add(new HealthCheckItem
            {
                Name        = "Sign-in Failures",
                Description = "Failed sign-in attempts in the last hour",
                Status      = failures > 50  ? HealthStatus.Critical
                            : failures > 10  ? HealthStatus.Warning
                            :                  HealthStatus.Healthy,
                Detail      = $"{failures} failed attempt(s) in the last hour",
            });
        }
        catch (Exception ex)
        {
            report.Checks.Add(new HealthCheckItem
            {
                Name        = "Sign-in Failures",
                Description = "Could not query sign-in history",
                Status      = HealthStatus.Warning,
                Detail      = ex.Message,
            });
        }

        // ── 4. Active sessions ───────────────────────────────────────────────
        try
        {
            report.ActiveSessions = await _sessions.GetActiveSessionCountAsync(ct);
            report.Checks.Add(new HealthCheckItem
            {
                Name        = "Active Sessions",
                Description = "Currently tracked user sessions",
                Status      = HealthStatus.Healthy,
                Detail      = $"{report.ActiveSessions} active session(s)",
            });
        }
        catch (Exception ex)
        {
            report.Checks.Add(new HealthCheckItem
            {
                Name        = "Active Sessions",
                Description = "Could not query session data",
                Status      = HealthStatus.Warning,
                Detail      = ex.Message,
            });
        }

        // ── 5. JWT configuration ─────────────────────────────────────────────
        report.JwtConfigured = !string.IsNullOrWhiteSpace(opts.Jwt.Issuer);
        report.Checks.Add(new HealthCheckItem
        {
            Name        = "JWT Configuration",
            Description = "JWT issuer and signing settings",
            Status      = report.JwtConfigured ? HealthStatus.Healthy : HealthStatus.Warning,
            Detail      = report.JwtConfigured
                              ? $"Issuer: {opts.Jwt.Issuer}"
                              : "JWT issuer is not configured",
        });

        // ── 6. OAuth configuration ───────────────────────────────────────────
        var oauth = opts.OAuth;
        report.OAuthConfigured =
            oauth.Google.Enabled    ||
            oauth.Microsoft.Enabled ||
            oauth.Apple.Enabled     ||
            oauth.GitHub.Enabled    ||
            oauth.CustomProviders.Any(p => !string.IsNullOrEmpty(p.ClientId));

        report.Checks.Add(new HealthCheckItem
        {
            Name        = "OAuth Providers",
            Description = "External identity provider configuration",
            Status      = HealthStatus.Healthy,
            Detail      = report.OAuthConfigured
                              ? "At least one OAuth provider is enabled"
                              : "No OAuth providers configured (optional)",
        });

        report.GeneratedAt = DateTimeOffset.UtcNow;
        return report;
    }
}
