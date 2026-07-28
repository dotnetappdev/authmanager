using AuthManager.Core.Options;

namespace AuthManager.Core.Services;

/// <summary>
/// Reads and updates the global (full-install) white-label branding — company name and
/// logo shown across the admin UI. Nothing here is secret, so unlike
/// <see cref="IPaymentSettingsService"/>/<see cref="ISmsSettingsService"/> there's no masking:
/// the full <see cref="BrandingOptions"/> round-trips as-is.
///
/// Per-tenant overrides are a separate concern — see <c>TenantDto.BrandingCompanyName</c>/
/// <c>BrandingLogoUrl</c>, managed through <see cref="ITenantService"/>.
/// </summary>
public interface IBrandingSettingsService
{
    /// <summary>Get the currently effective branding settings.</summary>
    Task<BrandingOptions> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>Persist updated branding settings.</summary>
    Task UpdateSettingsAsync(BrandingOptions settings, CancellationToken ct = default);
}
