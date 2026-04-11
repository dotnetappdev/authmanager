namespace AuthManager.Core.Options;

/// <summary>
/// Configuration options for DotNetAuthManager.
/// </summary>
public sealed class AuthManagerOptions
{
    /// <summary>
    /// The route prefix for the AuthManager UI. Default is "authmanager".
    /// Access the UI at /{RoutePrefix}
    /// </summary>
    public string RoutePrefix { get; set; } = "authmanager";

    /// <summary>
    /// The display title shown in the AuthManager UI.
    /// </summary>
    public string Title { get; set; } = "Auth Manager";

    /// <summary>
    /// The default theme. Defaults to System (follows OS preference).
    /// </summary>
    public AuthManagerTheme DefaultTheme { get; set; } = AuthManagerTheme.System;

    /// <summary>
    /// Require authentication to access the AuthManager UI.
    /// Strongly recommended in production.
    /// </summary>
    public bool RequireAuthentication { get; set; } = true;

    /// <summary>
    /// Roles allowed to access the AuthManager UI.
    /// If empty, any authenticated user can access it.
    /// </summary>
    public string[] AdminRoles { get; set; } = ["Admin"];

    /// <summary>
    /// Claims required to access the AuthManager UI.
    /// </summary>
    public Dictionary<string, string> RequiredClaims { get; set; } = [];

    /// <summary>
    /// JWT configuration options.
    /// </summary>
    public JwtOptions Jwt { get; set; } = new();

    /// <summary>
    /// Serilog log viewer options.
    /// </summary>
    public LogViewerOptions LogViewer { get; set; } = new();

    /// <summary>
    /// OAuth provider configurations.
    /// </summary>
    public OAuthOptions OAuth { get; set; } = new();

    /// <summary>
    /// Enable the audit log to track changes made via the UI.
    /// </summary>
    public bool EnableAuditLog { get; set; } = true;

    /// <summary>
    /// Maximum number of users to show per page.
    /// </summary>
    public int DefaultPageSize { get; set; } = 25;

    /// <summary>
    /// When true, ensures the SuperAdmin role exists and creates a default SuperAdmin user
    /// on startup if neither exists yet. A warning is logged when this runs.
    /// Defaults to false — opt-in only.
    /// </summary>
    public bool SeedSuperAdmin { get; set; } = false;

    /// <summary>
    /// Email for the seeded SuperAdmin account. Defaults to "superadmin@localhost".
    /// Ignored unless SeedSuperAdmin = true.
    /// </summary>
    public string SeedSuperAdminEmail { get; set; } = "superadmin@localhost";

    /// <summary>
    /// Initial password for the seeded SuperAdmin account. Defaults to a strong placeholder.
    /// Change this immediately after first login. Ignored unless SeedSuperAdmin = true.
    /// </summary>
    public string SeedSuperAdminPassword { get; set; } = "SuperAdmin@123456!";

    /// <summary>
    /// The role name that grants access to the AuthManager UI.
    /// Any user without this role will be denied — even authenticated users.
    /// Defaults to "SuperAdmin".
    /// </summary>
    public string SuperAdminRole { get; set; } = "SuperAdmin";

    /// <summary>
    /// Password complexity and rotation policy.
    /// Applied to ASP.NET Identity's <c>PasswordOptions</c> automatically.
    /// Configurable at runtime via /authmanager/security.
    /// </summary>
    public PasswordPolicyOptions PasswordPolicy { get; set; } = new();

    /// <summary>
    /// Account lockout and brute-force detection settings.
    /// Applied to ASP.NET Identity's <c>LockoutOptions</c> automatically.
    /// Configurable at runtime via /authmanager/security.
    /// </summary>
    public SecurityPolicyOptions SecurityPolicy { get; set; } = new();

    /// <summary>
    /// HTTP webhook endpoint configuration.
    /// Fire-and-forget signed HTTP POSTs on auth events.
    /// </summary>
    public WebhookOptions Webhooks { get; set; } = new();

    /// <summary>
    /// Singular display name for the user entity shown throughout the admin UI.
    /// E.g. "User" (default), "Member", "Customer", "Employee".
    /// Overridable at runtime via /authmanager/settings.
    /// </summary>
    public string UserEntityDisplayName { get; set; } = "User";

    /// <summary>
    /// Plural display name for the user entity.
    /// E.g. "Users" (default), "Members", "Customers", "Employees".
    /// </summary>
    public string UserEntityPluralDisplayName { get; set; } = "Users";

    /// <summary>
    /// Email notification settings — SMTP relay, sender info, and event triggers.
    /// </summary>
    public EmailNotificationOptions Email { get; set; } = new();

    /// <summary>
    /// Connection string for AuthManager's own internal database
    /// (stores audit entries, sessions, and settings overrides).
    /// Defaults to a local <c>authmanager.db</c> SQLite file.
    /// </summary>
    public string InternalDatabaseConnectionString { get; set; } = "Data Source=authmanager.db";

    /// <summary>
    /// Database provider for AuthManager's own internal data store.
    /// Supported values: <c>"SQLite"</c> (default, no install needed) or <c>"SqlServer"</c>.
    /// Configurable at runtime via the Security Settings UI.
    /// </summary>
    public string InternalDatabaseProvider { get; set; } = "SQLite";

    /// <summary>
    /// SSO (Single Sign-On) provider configurations — Entra ID, OIDC, SAML 2.0.
    /// </summary>
    public SsoOptions Sso { get; set; } = new();

    /// <summary>
    /// Email / SMS one-time password (OTP) settings for passwordless or step-up authentication.
    /// </summary>
    public OtpOptions Otp { get; set; } = new();
}

/// <summary>
/// Theme options for the AuthManager UI.
/// </summary>
public enum AuthManagerTheme
{
    Light,
    Dark,
    System
}

/// <summary>
/// JWT configuration options.
/// </summary>
public sealed class JwtOptions
{
    public bool EnableJwtManagement { get; set; } = true;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 7;
    public bool EnableRefreshTokens { get; set; } = true;
}

/// <summary>
/// Log viewer options.
/// </summary>
public sealed class LogViewerOptions
{
    public bool Enabled { get; set; } = true;
    public int MaxLogEntries { get; set; } = 10_000;
    public bool EnableLiveUpdates { get; set; } = true;
    public int LiveUpdateIntervalMs { get; set; } = 2000;
}

/// <summary>
/// OAuth provider options.
/// </summary>
public sealed class OAuthOptions
{
    public bool EnableOAuthManagement { get; set; } = true;
    public GoogleOAuthOptions Google { get; set; } = new();
    public MicrosoftOAuthOptions Microsoft { get; set; } = new();
    public AppleOAuthOptions Apple { get; set; } = new();
    public GitHubOAuthOptions GitHub { get; set; } = new();
    public List<CustomOAuthOptions> CustomProviders { get; set; } = [];
}

public sealed class GoogleOAuthOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public sealed class MicrosoftOAuthOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TenantId { get; set; } = "common";
}

public sealed class AppleOAuthOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string KeyId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
}

public sealed class GitHubOAuthOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public sealed class CustomOAuthOptions
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AuthorizationEndpoint { get; set; } = string.Empty;
    public string TokenEndpoint { get; set; } = string.Empty;
    public string UserInformationEndpoint { get; set; } = string.Empty;
}

// ── SSO ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Top-level SSO (Single Sign-On) options.
/// Supports Microsoft Entra ID (Azure AD), generic OIDC, and SAML 2.0.
/// </summary>
public sealed class SsoOptions
{
    /// <summary>Microsoft Entra ID (formerly Azure AD) OIDC/SAML integration.</summary>
    public EntraIdSsoOptions EntraId { get; set; } = new();

    /// <summary>Additional generic OIDC providers (Okta, Auth0, Keycloak, PingIdentity, etc.).</summary>
    public List<OidcSsoProviderOptions> OidcProviders { get; set; } = [];

    /// <summary>Generic SAML 2.0 identity provider settings.</summary>
    public SamlSsoOptions Saml { get; set; } = new();
}

/// <summary>
/// Microsoft Entra ID (formerly Azure Active Directory) SSO configuration.
/// Supports both OIDC and SAML 2.0 connection modes.
/// </summary>
public sealed class EntraIdSsoOptions
{
    /// <summary>Whether the Entra ID SSO integration is active.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Azure / Entra tenant ID.
    /// Use "common" to allow any Microsoft work/school or personal account,
    /// "organizations" for work/school only, "consumers" for personal only,
    /// or a specific GUID / domain for your tenant (recommended for production).
    /// </summary>
    public string TenantId { get; set; } = "common";

    /// <summary>Application (client) ID from the Entra ID app registration.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client secret from the Entra ID app registration credentials.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// OIDC authority URL. Defaults to the standard Entra ID endpoint.
    /// Override if using a national cloud (e.g. Azure Government).
    /// </summary>
    public string Authority { get; set; } = "https://login.microsoftonline.com/{tenantId}/v2.0";

    /// <summary>
    /// Extra OAuth 2.0 scopes to request in addition to openid/profile/email.
    /// Example: "User.Read GroupMember.Read.All"
    /// </summary>
    public string AdditionalScopes { get; set; } = string.Empty;

    /// <summary>
    /// Map Entra ID security group IDs or names to local Identity roles.
    /// Key = Entra group object ID, Value = local role name.
    /// </summary>
    public Dictionary<string, string> GroupToRoleMapping { get; set; } = [];

    /// <summary>
    /// When true, the groups claim is requested and group-to-role mapping is applied on sign-in.
    /// Requires "GroupMember.Read.All" scope and the groups claim enabled in the app manifest.
    /// </summary>
    public bool EnableGroupToRoleSync { get; set; }

    /// <summary>
    /// The callback URL registered in the Entra ID app registration.
    /// Typically https://yourapp.com/signin-microsoft or /signin-oidc.
    /// </summary>
    public string CallbackPath { get; set; } = "/signin-microsoft";
}

/// <summary>
/// A generic OIDC SSO provider (Okta, Auth0, Keycloak, PingFederate, etc.).
/// </summary>
public sealed class OidcSsoProviderOptions
{
    /// <summary>Unique internal key for this provider (used in routes and logs).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable label shown in the UI (e.g. "Okta", "Keycloak").</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Whether this provider is active.</summary>
    public bool Enabled { get; set; }

    /// <summary>OIDC authority / issuer URL (discovery document root).</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 client ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 client secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 callback path registered with the provider.</summary>
    public string CallbackPath { get; set; } = string.Empty;

    /// <summary>Space-separated additional scopes (openid profile email are always included).</summary>
    public string AdditionalScopes { get; set; } = string.Empty;

    /// <summary>Claim type used as the unique user identifier (default: "sub").</summary>
    public string UserIdClaim { get; set; } = "sub";
}

/// <summary>
/// Generic SAML 2.0 identity provider configuration.
/// </summary>
public sealed class SamlSsoOptions
{
    /// <summary>Whether SAML 2.0 SSO is enabled.</summary>
    public bool Enabled { get; set; }

    /// <summary>Your service provider entity ID (usually your app's base URL).</summary>
    public string ServiceProviderEntityId { get; set; } = string.Empty;

    /// <summary>The identity provider's SSO entry URL (SingleSignOnService Location).</summary>
    public string IdentityProviderSsoUrl { get; set; } = string.Empty;

    /// <summary>
    /// Identity provider's X.509 certificate (Base64-encoded DER) for validating assertions.
    /// </summary>
    public string IdentityProviderCertificate { get; set; } = string.Empty;

    /// <summary>SAML assertion consumer service (ACS) path registered at the IdP (e.g. /saml/acs).</summary>
    public string AssertionConsumerServicePath { get; set; } = "/saml/acs";

    /// <summary>Attribute name in the SAML assertion that carries the user's email.</summary>
    public string EmailAttributeName { get; set; } = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";

    /// <summary>Attribute name that carries the unique user identifier (NameID override).</summary>
    public string NameIdAttributeName { get; set; } = string.Empty;
}

// ── OTP ──────────────────────────────────────────────────────────────────────

/// <summary>
/// One-time password (OTP) options — email or SMS codes for passwordless login
/// and step-up / MFA verification flows.
/// </summary>
public sealed class OtpOptions
{
    /// <summary>Whether email-based OTP is enabled globally.</summary>
    public bool Enabled { get; set; }

    /// <summary>Length of the generated numeric code. Default: 6.</summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>
    /// How long a code remains valid after generation.
    /// Default: 10 minutes.
    /// </summary>
    public TimeSpan CodeExpiry { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Maximum number of failed verification attempts before the code is invalidated.
    /// Default: 5.
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Minimum time between code generation requests for the same user/purpose.
    /// Prevents spam. Default: 60 seconds.
    /// </summary>
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// When true, each code is alphanumeric (A–Z, 0–9) rather than purely numeric.
    /// Increases entropy but is harder to type on a phone.
    /// Default: false.
    /// </summary>
    public bool UseAlphanumericCodes { get; set; }

    /// <summary>
    /// Email subject line used when sending OTP codes.
    /// Supports {code} and {appName} placeholders.
    /// </summary>
    public string EmailSubject { get; set; } = "Your one-time code for {appName}";

    /// <summary>
    /// Email body template. Supports {code}, {expiry}, and {appName} placeholders.
    /// </summary>
    public string EmailBodyTemplate { get; set; } =
        "Your one-time code is: {code}\n\nThis code expires in {expiry}.\n\nIf you did not request this, please ignore this email.";
}
