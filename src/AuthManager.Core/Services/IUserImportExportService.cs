using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Bulk user import and export via CSV and JSON.
/// Supports importing from files and exporting filtered user sets.
/// </summary>
public interface IUserImportExportService
{
    // ── Export ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Export all users (optionally filtered by role) to a UTF-8 CSV byte array.
    /// Columns: Id, UserName, Email, EmailConfirmed, PhoneNumber, TwoFactorEnabled,
    ///          IsLockedOut, CreatedAt, LastLoginAt, Roles.
    /// </summary>
    Task<byte[]> ExportUsersCsvAsync(string? roleFilter = null, CancellationToken ct = default);

    /// <summary>
    /// Export all users to a JSON byte array suitable for backup or migration.
    /// </summary>
    Task<byte[]> ExportUsersJsonAsync(string? roleFilter = null, CancellationToken ct = default);

    // ── Import ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Import users from a CSV stream.
    ///
    /// Required columns: <c>Email</c>, <c>UserName</c>.
    /// Optional columns: <c>Password</c>, <c>EmailConfirmed</c>, <c>Roles</c> (pipe-separated).
    /// </summary>
    Task<ImportResult> ImportUsersCsvAsync(
        Stream csvStream,
        ImportOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Import users from a JSON stream (same schema as <see cref="ExportUsersJsonAsync"/>).
    /// </summary>
    Task<ImportResult> ImportUsersJsonAsync(
        Stream jsonStream,
        ImportOptions? options = null,
        CancellationToken ct = default);
}
