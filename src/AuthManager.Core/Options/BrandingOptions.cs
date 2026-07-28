namespace AuthManager.Core.Options;

/// <summary>
/// White-label branding for a "full install" (single-tenant) deployment — lets a host
/// label the AuthManager admin UI as their own product instead of "Auth Manager". Configurable
/// at runtime via /authmanager/settings; values set here are just the startup defaults, same
/// as <see cref="SecurityPolicyOptions"/>.
///
/// In multi-tenancy mode, an individual tenant can override <see cref="CompanyName"/> and
/// <see cref="LogoUrl"/> for its own users via <c>TenantDto.BrandingCompanyName</c>/
/// <c>BrandingLogoUrl</c> (managed on the Tenants dashboard) — these global values are the
/// fallback when a tenant hasn't set its own.
/// </summary>
public sealed class BrandingOptions
{
    /// <summary>
    /// Company/product name shown in the sidebar, setup wizard, and page titles.
    /// Falls back to <see cref="AuthManagerOptions.Title"/> when null/empty.
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// URL to a logo image shown in the sidebar header and setup wizard, replacing the
    /// default shield icon. Any size works — it's rendered at a fixed 32×32 box.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>Support contact shown on the setup wizard and error pages, if set.</summary>
    public string? SupportEmail { get; set; }

    /// <summary>
    /// When true, hides the "DotNetAuthManager · Docs &amp; GitHub" footer link on the setup
    /// wizard and the version tag in the sidebar — for a fully white-labeled install.
    /// </summary>
    public bool HidePoweredByFooter { get; set; }
}
