using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AuthManager.AspNetCore.Data;

/// <summary>
/// AuthManager's own internal database context for persisting settings,
/// audit entries, session data, and user field definitions. Separate from
/// the host application's DbContext — defaults to SQLite.
/// </summary>
public sealed class AuthManagerDbContext : DbContext
{
    public AuthManagerDbContext(DbContextOptions<AuthManagerDbContext> options) : base(options) { }

    /// <summary>
    /// SQLite's EF Core provider cannot translate ORDER BY, or relational comparisons
    /// (&gt;, &lt;), on <see cref="DateTimeOffset"/> columns — it stores them as text and
    /// throws <c>NotSupportedException</c> rather than risk an incorrect sort. Every
    /// timestamp/expiry column in this context is DateTimeOffset and gets ordered or
    /// range-compared somewhere (audit log, sessions, sign-in history, API tokens, OTP
    /// expiry…), so map them all to a sortable/comparable packed long instead — EF Core's
    /// documented workaround. SQL Server hosts are unaffected either way.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    public DbSet<AuthManagerSettingRecord>    Settings             { get; set; } = default!;
    public DbSet<AuditEntryRecord>            AuditEntries         { get; set; } = default!;
    public DbSet<AuthManagerSessionRecord>    Sessions             { get; set; } = default!;
    public DbSet<UserFieldDefinitionRecord>   UserFieldDefinitions { get; set; } = default!;
    public DbSet<SignInAttemptRecord>         SignInAttempts        { get; set; } = default!;
    public DbSet<ImpersonationTokenRecord>    ImpersonationTokens  { get; set; } = default!;
    public DbSet<GroupRecord>                 Groups               { get; set; } = default!;
    public DbSet<GroupMemberRecord>           GroupMembers         { get; set; } = default!;
    public DbSet<EmailTemplateRecord>         EmailTemplates       { get; set; } = default!;
    public DbSet<ApiTokenRecord>              ApiTokens            { get; set; } = default!;
    public DbSet<OtpRecord>                   OtpCodes             { get; set; } = default!;
    public DbSet<OAuthClientRecord>           OAuthClients         { get; set; } = default!;
    public DbSet<TenantRecord>                Tenants              { get; set; } = default!;
    public DbSet<CustomerRecord>              Customers            { get; set; } = default!;
    public DbSet<LicenseKeyRecord>            LicenseKeys          { get; set; } = default!;
    public DbSet<LicenseActivationRecord>     LicenseActivations   { get; set; } = default!;
    public DbSet<CustomerApiKeyRecord>        CustomerApiKeys      { get; set; } = default!;
    public DbSet<SubscriptionPlanRecord>      SubscriptionPlans    { get; set; } = default!;
    public DbSet<CustomerSubscriptionRecord>  CustomerSubscriptions { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AuthManagerSettingRecord>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(128);
            e.Property(x => x.ValueJson).HasMaxLength(4096);
        });

        b.Entity<AuditEntryRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.Action).HasMaxLength(128);
            e.Property(x => x.EntityType).HasMaxLength(128);
            e.Property(x => x.EntityId).HasMaxLength(256);
            e.Property(x => x.EntityName).HasMaxLength(256);
            e.Property(x => x.PerformedByUserId).HasMaxLength(450);
            e.Property(x => x.PerformedByUserName).HasMaxLength(256);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => x.Action);
        });

        b.Entity<AuthManagerSessionRecord>(e =>
        {
            e.HasKey(x => x.SessionId);
            e.Property(x => x.SessionId).HasMaxLength(128);
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.UserName).HasMaxLength(256);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.DeviceDescription).HasMaxLength(256);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.LastActiveAt);
        });

        b.Entity<UserFieldDefinitionRecord>(e =>
        {
            e.HasKey(x => x.FieldId);
            e.Property(x => x.FieldId).HasMaxLength(64);
            e.Property(x => x.DisplayName).HasMaxLength(128);
            e.Property(x => x.FieldType).HasMaxLength(32);
            e.Property(x => x.Placeholder).HasMaxLength(256);
            e.Property(x => x.DefaultValue).HasMaxLength(512);
            e.Property(x => x.SelectOptions).HasMaxLength(2048);
            e.Property(x => x.HelpText).HasMaxLength(512);
            e.HasIndex(x => x.SortOrder);
        });

        b.Entity<SignInAttemptRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.UserName).HasMaxLength(256);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.FailureReason).HasMaxLength(64);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.Property(x => x.UserAgent).HasMaxLength(512);
            e.HasIndex(x => x.Timestamp).IsDescending();
            e.HasIndex(x => new { x.UserId, x.Timestamp });
            e.HasIndex(x => new { x.Succeeded, x.Timestamp });
        });

        b.Entity<ImpersonationTokenRecord>(e =>
        {
            e.HasKey(x => x.Token);
            e.Property(x => x.Token).HasMaxLength(64);
            e.Property(x => x.AdminUserId).HasMaxLength(450);
            e.Property(x => x.TargetUserId).HasMaxLength(450);
            e.HasIndex(x => x.ExpiresAt);
        });

        b.Entity<GroupRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(512);
            e.Property(x => x.RolesJson).HasMaxLength(2048);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<GroupMemberRecord>(e =>
        {
            e.HasKey(x => new { x.GroupId, x.UserId });
            e.Property(x => x.GroupId).HasMaxLength(64);
            e.Property(x => x.UserId).HasMaxLength(450);
            e.HasIndex(x => x.UserId);
        });

        b.Entity<EmailTemplateRecord>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(64);
            e.Property(x => x.DisplayName).HasMaxLength(128);
            e.Property(x => x.Subject).HasMaxLength(256);
        });

        b.Entity<ApiTokenRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.TokenHash).HasMaxLength(128);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.Prefix).HasMaxLength(8);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        b.Entity<OtpRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.Purpose).HasMaxLength(64);
            e.Property(x => x.CodeHash).HasMaxLength(128);
            e.HasIndex(x => new { x.UserId, x.Purpose });
            e.HasIndex(x => x.ExpiresAt);
        });

        b.Entity<TenantRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.DisplayName).HasMaxLength(128);
            e.Property(x => x.MetadataJson).HasMaxLength(2048);
            e.Property(x => x.BrandingCompanyName).HasMaxLength(128);
            e.Property(x => x.BrandingLogoUrl).HasMaxLength(1024);
            e.Property(x => x.FeatureFlagsJson).HasMaxLength(1024);
        });

        b.Entity<OAuthClientRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.ClientId).HasMaxLength(128);
            e.Property(x => x.SecretHash).HasMaxLength(128);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(512);
            e.Property(x => x.ScopesJson).HasMaxLength(2048);
            e.HasIndex(x => x.ClientId).IsUnique();
        });

        b.Entity<CustomerRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.CompanyName).HasMaxLength(256);
            e.Property(x => x.Notes).HasMaxLength(2048);
            e.HasIndex(x => x.Email);
        });

        b.Entity<LicenseKeyRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Key).HasMaxLength(32);
            e.Property(x => x.ProductName).HasMaxLength(128);
            e.Property(x => x.CustomerId).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.Notes).HasMaxLength(2048);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.CustomerId);
        });

        b.Entity<LicenseActivationRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.LicenseKeyId).HasMaxLength(64);
            e.Property(x => x.MachineId).HasMaxLength(256);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => new { x.LicenseKeyId, x.MachineId }).IsUnique();
        });

        b.Entity<CustomerApiKeyRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.CustomerId).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Prefix).HasMaxLength(16);
            e.Property(x => x.KeyHash).HasMaxLength(128);
            e.Property(x => x.ScopesJson).HasMaxLength(2048);
            e.HasIndex(x => x.KeyHash).IsUnique();
            e.HasIndex(x => x.CustomerId);
        });

        b.Entity<SubscriptionPlanRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(128);
            e.Property(x => x.Description).HasMaxLength(1024);
            e.Property(x => x.Currency).HasMaxLength(8);
            e.Property(x => x.Interval).HasMaxLength(16);
            e.Property(x => x.FeaturesJson).HasMaxLength(2048);
            e.HasIndex(x => x.Name).IsUnique();
        });

        b.Entity<CustomerSubscriptionRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.CustomerId).HasMaxLength(64);
            e.Property(x => x.PlanId).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.PaymentProvider).HasMaxLength(16);
            e.Property(x => x.ExternalCustomerId).HasMaxLength(128);
            e.Property(x => x.ExternalSubscriptionId).HasMaxLength(128);
            e.Property(x => x.ExternalCheckoutSessionId).HasMaxLength(128);
            e.HasIndex(x => x.CustomerId);
        });
    }
}

/// <summary>Named group of roles.</summary>
public sealed class GroupRecord
{
    public string   Id          { get; set; } = Guid.NewGuid().ToString("N")[..16];
    public string   Name        { get; set; } = string.Empty;
    public string?  Description { get; set; }
    public string   RolesJson   { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>User membership in a group.</summary>
public sealed class GroupMemberRecord
{
    public string GroupId  { get; set; } = string.Empty;
    public string UserId   { get; set; } = string.Empty;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Transactional email template stored in the internal DB.</summary>
public sealed class EmailTemplateRecord
{
    public string  Key         { get; set; } = string.Empty;
    public string  DisplayName { get; set; } = string.Empty;
    public string  Subject     { get; set; } = string.Empty;
    public string  BodyHtml    { get; set; } = string.Empty;
    public string  BodyText    { get; set; } = string.Empty;
    public bool    IsEnabled   { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Long-lived personal API token (stored as a SHA-256 hash).</summary>
public sealed class ApiTokenRecord
{
    public string  Id         { get; set; } = string.Empty;
    public string  Prefix     { get; set; } = string.Empty;   // first 8 chars shown to user
    public string  TokenHash  { get; set; } = string.Empty;   // SHA-256 of the full token
    public string  Name       { get; set; } = string.Empty;
    public string  UserId     { get; set; } = string.Empty;
    public string? Scopes     { get; set; }
    public bool    IsRevoked  { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt  { get; set; }
}

/// <summary>A multi-tenancy tenant. Membership is tracked via the user's tenant claim, not a join table.</summary>
public sealed class TenantRecord
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Per-tenant white-label override — falls back to the global BrandingOptions.CompanyName when null.</summary>
    public string? BrandingCompanyName { get; set; }

    /// <summary>Per-tenant logo override — falls back to the global BrandingOptions.LogoUrl when null.</summary>
    public string? BrandingLogoUrl { get; set; }

    /// <summary>JSON dictionary of TenantFeature name -&gt; bool overrides (see TenantFeature). Unset features inherit the global default.</summary>
    public string FeatureFlagsJson { get; set; } = "{}";
}

/// <summary>A registered OAuth2 client application (service-to-service, client-credentials grant).</summary>
public sealed class OAuthClientRecord
{
    public string  Id          { get; set; } = Guid.NewGuid().ToString("N")[..16];
    public string  ClientId    { get; set; } = string.Empty;
    public string  SecretHash  { get; set; } = string.Empty;
    public string  Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool    Enabled     { get; set; } = true;
    public string  ScopesJson  { get; set; } = "[]";
    public DateTimeOffset  CreatedAt  { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
}

/// <summary>Key/value store for serialised settings overrides.</summary>
public sealed class AuthManagerSettingRecord
{
    [MaxLength(128)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(4096)]
    public string ValueJson { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Persisted audit entry row.</summary>
public sealed class AuditEntryRecord
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? PerformedByUserId { get; set; }
    public string? PerformedByUserName { get; set; }
    public string? IpAddress { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

/// <summary>Persisted session row.</summary>
public sealed class AuthManagerSessionRecord
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastActiveAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceDescription { get; set; }
}

/// <summary>Persisted user field definition row.</summary>
public sealed class UserFieldDefinitionRecord
{
    public string FieldId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FieldType { get; set; } = "Text";
    public bool IsRequired { get; set; }
    public string? Placeholder { get; set; }
    public string? DefaultValue { get; set; }
    public string? SelectOptions { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public string? HelpText { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Persisted sign-in attempt row.</summary>
public sealed class SignInAttemptRecord
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public bool Succeeded { get; set; }
    public string? FailureReason { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>One-time impersonation token record.</summary>
public sealed class ImpersonationTokenRecord
{
    public string Token { get; set; } = string.Empty;
    public string AdminUserId { get; set; } = string.Empty;
    public string TargetUserId { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>An external customer/account that licenses, API keys, and subscriptions attach to.</summary>
public sealed class CustomerRecord
{
    public string  Id          { get; set; } = Guid.NewGuid().ToString("N")[..16];
    public string  Name        { get; set; } = string.Empty;
    public string  Email       { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Notes       { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A software license / product key ("CD key"), stored in plain text — unlike API
/// tokens/OAuth secrets, a license key must be human-readable and re-displayable (an admin
/// resending it, an installer prompting for it again), so it isn't hashed.</summary>
public sealed class LicenseKeyRecord
{
    public string  Id              { get; set; } = Guid.NewGuid().ToString("N")[..16];
    public string  Key             { get; set; } = string.Empty;
    public string  ProductName     { get; set; } = string.Empty;
    public string? CustomerId      { get; set; }
    public int     MaxActivations  { get; set; } = 1;
    public string  Status          { get; set; } = "Active"; // Active | Revoked | Expired
    public string? Notes           { get; set; }
    public DateTimeOffset  IssuedAt   { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt  { get; set; }
    public DateTimeOffset? RevokedAt  { get; set; }
}

/// <summary>A single machine/device activation against a license key.</summary>
public sealed class LicenseActivationRecord
{
    public string  Id           { get; set; } = Guid.NewGuid().ToString("N")[..16];
    public string  LicenseKeyId { get; set; } = string.Empty;
    public string  MachineId    { get; set; } = string.Empty;
    public string? IpAddress    { get; set; }
    public DateTimeOffset ActivatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>A customer-facing API key (bearer key handed to a customer's own application).</summary>
public sealed class CustomerApiKeyRecord
{
    public string  Id          { get; set; } = Guid.NewGuid().ToString("N")[..16];
    public string  CustomerId  { get; set; } = string.Empty;
    public string  Name        { get; set; } = string.Empty;
    public string  Prefix      { get; set; } = string.Empty;
    public string  KeyHash     { get; set; } = string.Empty;
    public string  ScopesJson  { get; set; } = "[]";
    public int?    RateLimitPerMinute { get; set; }
    public bool    Enabled     { get; set; } = true;
    public DateTimeOffset  CreatedAt  { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? ExpiresAt  { get; set; }
}

/// <summary>A plan customers can subscribe to.</summary>
public sealed class SubscriptionPlanRecord
{
    public string  Id           { get; set; } = Guid.NewGuid().ToString("N")[..16];
    public string  Name         { get; set; } = string.Empty;
    public string? Description  { get; set; }
    public int     PriceCents   { get; set; }
    public string  Currency     { get; set; } = "USD";
    public string  Interval     { get; set; } = "Monthly"; // Monthly | Yearly | Weekly | OneTime
    public int     TrialDays    { get; set; }
    public int?    MaxApiKeys   { get; set; }
    public int?    MaxActivations { get; set; }
    public string  FeaturesJson { get; set; } = "[]";
    public bool    IsActive     { get; set; } = true;
}

/// <summary>A customer's subscription to a plan.</summary>
public sealed class CustomerSubscriptionRecord
{
    public string  Id          { get; set; } = Guid.NewGuid().ToString("N")[..16];
    public string  CustomerId  { get; set; } = string.Empty;
    public string  PlanId      { get; set; } = string.Empty;
    public string  Status      { get; set; } = "Active"; // Trialing | Active | PastDue | Canceled | Expired
    public DateTimeOffset  StartedAt        { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? TrialEndsAt      { get; set; }
    public DateTimeOffset  CurrentPeriodEnd { get; set; }
    public DateTimeOffset? CanceledAt       { get; set; }

    /// <summary>"None" (internal/manual billing) | "Stripe" | "PayPal". Default: "None".</summary>
    public string  PaymentProvider          { get; set; } = "None";

    /// <summary>Stripe Customer ID or PayPal Payer ID, once a checkout has been started/completed.</summary>
    public string? ExternalCustomerId       { get; set; }

    /// <summary>Stripe Subscription ID or PayPal Subscription/Order ID.</summary>
    public string? ExternalSubscriptionId   { get; set; }

    /// <summary>The Stripe Checkout Session ID or PayPal Order ID that started this subscription, for correlating the return-URL redirect back to a webhook confirmation.</summary>
    public string? ExternalCheckoutSessionId { get; set; }
}

/// <summary>
/// Persisted one-time password (OTP) code record.
/// The plain-text code is never stored — only its SHA-256 hash.
/// </summary>
public sealed class OtpRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..16];

    /// <summary>Identity user ID this code belongs to.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Logical purpose: "login", "email-verify", "step-up", etc.</summary>
    public string Purpose { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the plain-text OTP code.</summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>Number of failed verification attempts so far.</summary>
    public int Attempts { get; set; }

    /// <summary>Whether the code has been successfully used (consumed).</summary>
    public bool IsUsed { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
