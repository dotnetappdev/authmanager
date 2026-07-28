using AuthManager.AspNetCore.Data;
using MudBlazor.Services;
using AuthManager.AspNetCore.Seeding;
using AuthManager.AspNetCore.Services;
using AuthManager.AspNetCore.Storage;
using AuthManager.Core.Options;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AuthManager.AspNetCore.Extensions;

/// <summary>
/// Extension methods for adding DotNetAuthManager to the DI container.
///
/// AuthManager does not manage your database — it works with whatever
/// DbContext + Identity setup you already have. Call AddAuthManager()
/// after your own AddIdentity() call.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AuthManager on top of your existing ASP.NET Identity setup.
    ///
    /// Prerequisites — call these before AddAuthManager():
    ///   builder.Services.AddDbContext&lt;AppDbContext&gt;(...);
    ///   builder.Services.AddIdentity&lt;ApplicationUser, IdentityRole&gt;()
    ///                   .AddEntityFrameworkStores&lt;AppDbContext&gt;();
    /// </summary>
    public static IServiceCollection AddAuthManager<TUser>(
        this IServiceCollection services,
        Action<AuthManagerOptions>? configure = null)
        where TUser : IdentityUser, new()
        => services.AddAuthManager<TUser, IdentityRole>(configure);

    /// <summary>
    /// Adds AuthManager with a custom role type.
    /// </summary>
    public static IServiceCollection AddAuthManager<TUser, TRole>(
        this IServiceCollection services,
        Action<AuthManagerOptions>? configure = null)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
    {
        var optBuilder = services.AddOptions<AuthManagerOptions>();
        if (configure != null)
            optBuilder.Configure(configure);

        // Read the Storage choice synchronously — AddIdentity() (self-contained mode only)
        // has to happen right now, as part of this call, not lazily behind IOptions.
        var previewOptions = new AuthManagerOptions();
        configure?.Invoke(previewOptions);

        if (previewOptions.Storage.Provider == AuthManagerStorageProvider.SelfContained)
        {
            AddSelfContainedStorage<TUser, TRole>(services, previewOptions.Storage);
        }

        // ── AuthManager's own internal database (SQLite by default) ──────────
        // Registered with AddDbContextFactory so singleton services can create
        // short-lived scopes without holding an open connection.
        services.AddDbContextFactory<AuthManagerDbContext>((sp, opts) =>
        {
            var authOpts = sp.GetService<IOptions<AuthManagerOptions>>()?.Value
                           ?? new AuthManagerOptions();

            var cs       = authOpts.InternalDatabaseConnectionString;
            var provider = authOpts.InternalDatabaseProvider;

            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                opts.UseSqlServer(cs);
            else
                opts.UseSqlite(cs);
        });

        // Ensure the schema exists on first use (no migrations required)
        services.AddHostedService<AuthManagerDbInitialiser>();

        // ── Core singletons ──────────────────────────────────────────────────
        services.TryAddSingleton<LogAggregationService>();
        services.TryAddSingleton<ILogAggregationService>(
            sp => sp.GetRequiredService<LogAggregationService>());

        // DB-backed audit, security-policy, session, field, naming, and settings services
        services.TryAddSingleton<IAuditService, PersistentAuditService>();
        services.TryAddSingleton<ISessionService, PersistentSessionService>();
        services.TryAddSingleton<ISecurityPolicyService, PersistentSecurityPolicyService>();
        services.TryAddSingleton<IUserFieldService, UserFieldService>();
        services.TryAddSingleton<IEntityNamingService, EntityNamingService>();
        services.TryAddSingleton<ISettingsStore, SettingsStore>();

        // Sign-in history — singleton (uses IDbContextFactory)
        services.TryAddSingleton<ISignInHistoryService, SignInHistoryService>();

        // ── Scoped (one per Blazor circuit) ──────────────────────────────────
        services.TryAddScoped<IUserManagementService, UserManagementService<TUser>>();
        services.TryAddScoped<IRoleManagementService, RoleManagementService<TRole>>();
        services.TryAddScoped<IOAuthProviderService, OAuthProviderService>();
        services.TryAddScoped<IJwtConfigService, JwtConfigService>();
        services.TryAddScoped<IUserImportExportService, UserImportExportService<TUser>>();
        services.TryAddScoped<IImpersonationService, ImpersonationService<TUser>>();
        services.TryAddScoped<ISystemHealthService, SystemHealthService>();
        services.TryAddScoped<ISsoService, SsoService>();
        services.TryAddScoped<IOtpService, OtpService>();

        // Webhook dispatcher — requires HttpClient
        services.TryAddScoped<IWebhookService, WebhookService>();
        services.AddHttpClient("AuthManager.Webhooks");

        // Blazor — idempotent if host already called AddRazorComponents()
        services.AddRazorComponents()
                .AddInteractiveServerComponents();

        // MudBlazor — required for the admin UI components (idempotent)
        services.AddMudServices();

        services.AddHttpContextAccessor();

        // Apply PasswordPolicy and SecurityPolicy to ASP.NET Identity at startup
        services.PostConfigure<PasswordOptions>(opts =>
        {
            using var sp     = services.BuildServiceProvider();
            var authOpts     = sp.GetService<IOptions<AuthManagerOptions>>()?.Value;
            if (authOpts is null) return;
            var pp           = authOpts.PasswordPolicy;
            opts.RequiredLength         = pp.MinimumLength;
            opts.RequireUppercase       = pp.RequireUppercase;
            opts.RequireLowercase       = pp.RequireLowercase;
            opts.RequireDigit           = pp.RequireDigit;
            opts.RequireNonAlphanumeric = pp.RequireNonAlphanumeric;
        });

        services.PostConfigure<LockoutOptions>(opts =>
        {
            using var sp     = services.BuildServiceProvider();
            var authOpts     = sp.GetService<IOptions<AuthManagerOptions>>()?.Value;
            if (authOpts is null) return;
            var sec          = authOpts.SecurityPolicy;
            opts.AllowedForNewUsers      = sec.EnableBruteForceDetection;
            opts.MaxFailedAccessAttempts = sec.MaxFailedLoginAttempts;
            opts.DefaultLockoutTimeSpan  = sec.LockoutDuration;
        });

        // First-run setup service
        services.TryAddScoped<ISetupService, SetupService<TUser, TRole>>();

        // Groups, API tokens, and TOTP challenge
        services.TryAddScoped<IGroupService,         GroupService<TUser, TRole>>();
        services.TryAddScoped<IApiTokenService,      ApiTokenService<TUser>>();
        services.TryAddScoped<ITotpChallengeService, TotpChallengeService<TUser>>();
        services.TryAddScoped<ITenantService,        TenantService<TUser>>();
        services.TryAddScoped<IOAuthClientService,   OAuthClientService>();

        // Licensing & monetization — customers, license keys, customer-facing API keys, subscriptions
        services.TryAddScoped<ICustomerService,        CustomerService>();
        services.TryAddScoped<ILicenseService,         LicenseService>();
        services.TryAddScoped<ICustomerApiKeyService,  CustomerApiKeyService>();
        services.TryAddScoped<ISubscriptionService,    SubscriptionService>();

        // Passkeys (WebAuthn) — wraps SignInManager/UserManager's native ceremony methods
        services.TryAddScoped<IPasskeyService, PasskeyService<TUser>>();

        // Optional SuperAdmin seeder — only acts when options.SeedSuperAdmin = true
        services.AddHostedService<SuperAdminSeeder<TUser, TRole>>();

        // Revokes temporary (expiring) role assignments once they lapse
        services.AddHostedService<RoleExpirySweeperService<TUser>>();

        return services;
    }

    /// <summary>
    /// Wires up AuthManager's self-contained storage provider: its own database, its own
    /// <c>AddIdentity&lt;TUser, TRole&gt;()</c> registration (so the host doesn't have to),
    /// and its own PBKDF2-HMACSHA256 password hasher. Everything downstream — every
    /// AuthManager service, the REST API, the Blazor UI — keeps using
    /// <c>UserManager&lt;TUser&gt;</c>/<c>RoleManager&lt;TRole&gt;</c> exactly as it does in
    /// the default <see cref="AuthManagerStorageProvider.AspNetIdentity"/> mode; only what's
    /// underneath those managers changes.
    /// </summary>
    private static void AddSelfContainedStorage<TUser, TRole>(IServiceCollection services, AuthManagerStorageOptions storage)
        where TUser : IdentityUser, new()
        where TRole : IdentityRole, new()
    {
        services.AddDbContext<SelfContainedIdentityDbContext<TUser, TRole>>(opts =>
        {
            if (storage.DatabaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                opts.UseSqlServer(storage.ConnectionString);
            else
                opts.UseSqlite(storage.ConnectionString);
        });

        services.AddIdentity<TUser, TRole>()
                .AddEntityFrameworkStores<SelfContainedIdentityDbContext<TUser, TRole>>()
                .AddDefaultTokenProviders();

        // AddIdentity() registers Identity's own PasswordHasher<TUser> with TryAdd — Replace
        // guarantees ours wins regardless of call order.
        services.Replace(ServiceDescriptor.Singleton<IPasswordHasher<TUser>>(sp =>
        {
            var iterations = sp.GetRequiredService<IOptions<AuthManagerOptions>>().Value.Storage.Pbkdf2Iterations;
            return new SelfContainedPasswordHasher<TUser>(iterations);
        }));

        services.AddHostedService<SelfContainedIdentityDbInitialiser<TUser, TRole>>();
    }
}
