using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuthManager.AspNetCore.Storage;

/// <summary>
/// Ensures the self-contained identity database schema exists on startup. Uses
/// <c>EnsureCreated</c> — no migrations required, mirroring how AuthManager initialises its
/// own internal database (see <c>AuthManagerDbInitialiser</c>).
/// </summary>
internal sealed class SelfContainedIdentityDbInitialiser<TUser, TRole> : IHostedService
    where TUser : IdentityUser
    where TRole : IdentityRole
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SelfContainedIdentityDbInitialiser<TUser, TRole>> _logger;

    public SelfContainedIdentityDbInitialiser(
        IServiceProvider services,
        ILogger<SelfContainedIdentityDbInitialiser<TUser, TRole>> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<SelfContainedIdentityDbContext<TUser, TRole>>();
            await db.Database.EnsureCreatedAsync(cancellationToken);
            _logger.LogInformation("[DotNetAuthManager] Self-contained identity database ready.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[DotNetAuthManager] Failed to initialise the self-contained identity database. " +
                "Check AuthManagerOptions.Storage.ConnectionString / DatabaseProvider.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
