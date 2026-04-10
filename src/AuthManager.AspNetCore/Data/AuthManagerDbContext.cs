using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Data;

/// <summary>
/// AuthManager's own internal database context for persisting settings,
/// audit entries, and session data. Separate from the host application's
/// DbContext — defaults to SQLite so no extra server is required.
/// </summary>
public sealed class AuthManagerDbContext : DbContext
{
    public AuthManagerDbContext(DbContextOptions<AuthManagerDbContext> options) : base(options) { }

    public DbSet<AuthManagerSettingRecord> Settings { get; set; } = default!;
    public DbSet<AuditEntryRecord> AuditEntries { get; set; } = default!;
    public DbSet<AuthManagerSessionRecord> Sessions { get; set; } = default!;

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
    }
}

/// <summary>Key/value store for serialised settings overrides (e.g. PasswordPolicy, SecurityPolicy).</summary>
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
