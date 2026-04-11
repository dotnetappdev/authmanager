using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Data;

/// <summary>
/// AuthManager's own internal database context for persisting settings,
/// audit entries, session data, and user field definitions. Separate from
/// the host application's DbContext — defaults to SQLite.
/// </summary>
public sealed class AuthManagerDbContext : DbContext
{
    public AuthManagerDbContext(DbContextOptions<AuthManagerDbContext> options) : base(options) { }

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
