using AuthManager.AspNetCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuthManager.AspNetCore.Seeding;

/// <summary>
/// Ensures the AuthManager internal database schema exists on startup.
/// Uses <c>EnsureCreated</c> — no migrations required.
/// </summary>
internal sealed class AuthManagerDbInitialiser : IHostedService
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;
    private readonly ILogger<AuthManagerDbInitialiser>       _logger;

    public AuthManagerDbInitialiser(
        IDbContextFactory<AuthManagerDbContext> factory,
        ILogger<AuthManagerDbInitialiser> logger)
    {
        _factory = factory;
        _logger  = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            await db.Database.EnsureCreatedAsync(cancellationToken);
            _logger.LogInformation("AuthManager internal database ready.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AuthManager internal database initialisation failed. " +
                "Audit log, sessions, and settings will fall back to in-memory storage.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
