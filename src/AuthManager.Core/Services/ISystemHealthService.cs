using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

public interface ISystemHealthService
{
    Task<SystemHealthReport> GetHealthAsync(CancellationToken ct = default);
}
