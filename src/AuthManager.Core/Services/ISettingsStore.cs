namespace AuthManager.Core.Services;

/// <summary>
/// Generic key-value settings store backed by the internal AuthManager database.
/// Used to persist runtime overrides (email config, display settings, etc.).
/// </summary>
public interface ISettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
    Task<Dictionary<string, string>> GetByPrefixAsync(string prefix, CancellationToken ct = default);
}
