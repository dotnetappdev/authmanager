using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Manages the one-time first-run setup state.
/// Once <see cref="CompleteSetupAsync"/> is called successfully the flag is
/// persisted and <see cref="IsSetupCompleteAsync"/> returns <c>true</c> forever.
/// </summary>
public interface ISetupService
{
    /// <summary>Returns true when the initial setup has been completed.</summary>
    Task<bool> IsSetupCompleteAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates the SuperAdmin role + user, applies password/security policy,
    /// and marks setup complete in the internal database.
    /// </summary>
    Task<(bool Success, string[] Errors)> CompleteSetupAsync(
        SetupWizardDto dto, CancellationToken ct = default);
}
