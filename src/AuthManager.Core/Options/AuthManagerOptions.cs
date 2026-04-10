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
