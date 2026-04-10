using System.Text;
using System.Text.Json;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// CSV and JSON import/export for users.
/// Equivalent to bulk CSV/JSON user import/export and Firebase <c>importUsers</c>.
/// </summary>
public sealed class UserImportExportService<TUser> : IUserImportExportService
    where TUser : IdentityUser, new()
{
    private readonly UserManager<TUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly ILogger<UserImportExportService<TUser>> _logger;

    private static readonly string[] CsvColumns =
    [
        "Id", "UserName", "Email", "EmailConfirmed",
        "PhoneNumber", "TwoFactorEnabled", "IsLockedOut",
        "CreatedAt", "LastLoginAt", "Roles"
    ];

    public UserImportExportService(
        UserManager<TUser> users,
        RoleManager<IdentityRole> roles,
        ILogger<UserImportExportService<TUser>> logger)
    {
        _users  = users;
        _roles  = roles;
        _logger = logger;
    }

    // ── Export ────────────────────────────────────────────────────────────────

    public async Task<byte[]> ExportUsersCsvAsync(
        string? roleFilter = null, CancellationToken ct = default)
    {
        var users = await GetFilteredUsersAsync(roleFilter, ct);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", CsvColumns));

        foreach (var (user, roles) in users)
        {
            sb.AppendLine(string.Join(",",
                Csv(user.Id),
                Csv(user.UserName ?? string.Empty),
                Csv(user.Email    ?? string.Empty),
                Csv(user.EmailConfirmed.ToString()),
                Csv(user.PhoneNumber   ?? string.Empty),
                Csv(user.TwoFactorEnabled.ToString()),
                Csv((user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow).ToString()),
                Csv(string.Empty),   // CreatedAt — not on base IdentityUser
                Csv(string.Empty),   // LastLoginAt — extension point
                Csv(string.Join("|", roles))
            ));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportUsersJsonAsync(
        string? roleFilter = null, CancellationToken ct = default)
    {
        var users = await GetFilteredUsersAsync(roleFilter, ct);

        var records = users.Select(u => new
        {
            id             = u.user.Id,
            userName       = u.user.UserName,
            email          = u.user.Email,
            emailConfirmed = u.user.EmailConfirmed,
            phoneNumber    = u.user.PhoneNumber,
            twoFactor      = u.user.TwoFactorEnabled,
            isLockedOut    = u.user.LockoutEnd.HasValue && u.user.LockoutEnd > DateTimeOffset.UtcNow,
            roles          = u.roles
        }).ToList();

        var options = new JsonSerializerOptions { WriteIndented = true };
        return JsonSerializer.SerializeToUtf8Bytes(records, options);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    public async Task<ImportResult> ImportUsersCsvAsync(
        Stream csvStream,
        ImportOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new ImportOptions();
        var result = new ImportResult();

        using var reader = new StreamReader(csvStream, Encoding.UTF8, leaveOpen: true);
        var header = await reader.ReadLineAsync(ct);
        if (header is null) return result;

        var columns = header.Split(',').Select(c => c.Trim('"').Trim()).ToArray();
        int emailIdx    = Array.IndexOf(columns, "Email");
        int userNameIdx = Array.IndexOf(columns, "UserName");
        int passwordIdx = Array.IndexOf(columns, "Password");
        int rolesIdx    = Array.IndexOf(columns, "Roles");
        int confirmedIdx = Array.IndexOf(columns, "EmailConfirmed");

        if (emailIdx < 0 || userNameIdx < 0)
        {
            result.Errors.Add("CSV must contain 'Email' and 'UserName' columns.");
            result.Failed++;
            return result;
        }

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var cols = ParseCsvLine(line);

            var email    = Get(cols, emailIdx);
            var userName = Get(cols, userNameIdx);
            if (string.IsNullOrEmpty(email)) { result.Skipped++; continue; }

            await ProcessImportRowAsync(
                email, userName,
                passwordIdx >= 0 ? Get(cols, passwordIdx) : null,
                confirmedIdx >= 0 ? Get(cols, confirmedIdx).Equals("true", StringComparison.OrdinalIgnoreCase) : false,
                rolesIdx >= 0 ? Get(cols, rolesIdx).Split('|', StringSplitOptions.RemoveEmptyEntries) : [],
                options, result, ct);
        }

        return result;
    }

    public async Task<ImportResult> ImportUsersJsonAsync(
        Stream jsonStream,
        ImportOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new ImportOptions();
        var result = new ImportResult();

        List<JsonElement>? records;
        try
        {
            records = await JsonSerializer.DeserializeAsync<List<JsonElement>>(jsonStream, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Invalid JSON: {ex.Message}");
            result.Failed++;
            return result;
        }

        if (records is null) return result;

        foreach (var record in records)
        {
            var email    = record.TryGetProperty("email",    out var em) ? em.GetString() ?? "" : "";
            var userName = record.TryGetProperty("userName", out var un) ? un.GetString() ?? "" : "";
            var password = record.TryGetProperty("password", out var pw) ? pw.GetString() : null;
            var confirmed = record.TryGetProperty("emailConfirmed", out var ec) && ec.GetBoolean();
            var roles    = record.TryGetProperty("roles", out var rl)
                ? rl.EnumerateArray().Select(r => r.GetString() ?? "").Where(r => r != "").ToArray()
                : Array.Empty<string>();

            if (string.IsNullOrEmpty(email)) { result.Skipped++; continue; }
            await ProcessImportRowAsync(email, userName, password, confirmed, roles, options, result, ct);
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task ProcessImportRowAsync(
        string email, string userName, string? password, bool emailConfirmed,
        IEnumerable<string> roles, ImportOptions options, ImportResult result, CancellationToken ct)
    {
        var existing = await _users.FindByEmailAsync(email);

        if (existing is not null)
        {
            if (!options.UpdateExisting) { result.Skipped++; return; }

            existing.UserName       = string.IsNullOrEmpty(userName) ? existing.UserName : userName;
            existing.EmailConfirmed = emailConfirmed;
            var updateResult = await _users.UpdateAsync(existing);
            if (!updateResult.Succeeded)
            {
                result.Failed++;
                result.Errors.Add($"Update failed for {email}: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
                return;
            }
            result.Updated++;
        }
        else
        {
            var user = new TUser
            {
                UserName       = string.IsNullOrEmpty(userName) ? email.Split('@')[0] : userName,
                Email          = email,
                EmailConfirmed = emailConfirmed
            };

            IdentityResult createResult = string.IsNullOrEmpty(password)
                ? await _users.CreateAsync(user)
                : await _users.CreateAsync(user, password);

            if (!createResult.Succeeded)
            {
                result.Failed++;
                result.Errors.Add($"Create failed for {email}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                return;
            }

            existing = await _users.FindByEmailAsync(email);
            result.Created++;
        }

        // Assign roles
        if (existing is not null)
        {
            var allRoles = options.DefaultRoles.Concat(roles).Distinct();
            foreach (var role in allRoles)
            {
                if (!await _users.IsInRoleAsync(existing, role))
                    await _users.AddToRoleAsync(existing, role);
            }
        }
    }

    private async Task<List<(TUser user, List<string> roles)>> GetFilteredUsersAsync(
        string? roleFilter, CancellationToken ct)
    {
        var allUsers = string.IsNullOrEmpty(roleFilter)
            ? _users.Users.ToList()
            : (await _users.GetUsersInRoleAsync(roleFilter)).ToList();

        var result = new List<(TUser, List<string>)>();
        foreach (var user in allUsers)
        {
            var roles = (await _users.GetRolesAsync(user)).ToList();
            result.Add((user, roles));
        }
        return result;
    }

    private static string Csv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static string Get(string[] cols, int idx)
        => idx >= 0 && idx < cols.Length ? cols[idx].Trim('"').Trim() : string.Empty;

    private static string[] ParseCsvLine(string line)
    {
        var cols = new List<string>();
        var current = new StringBuilder();
        bool inQuote = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                { current.Append('"'); i++; }
                else
                { inQuote = !inQuote; }
            }
            else if (c == ',' && !inQuote)
            { cols.Add(current.ToString()); current.Clear(); }
            else
            { current.Append(c); }
        }
        cols.Add(current.ToString());
        return cols.ToArray();
    }
}
