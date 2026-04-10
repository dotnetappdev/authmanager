using System.Text.RegularExpressions;
using AuthManager.AspNetCore.Data;
using AuthManager.Core.Models;
using AuthManager.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AuthManager.AspNetCore.Services;

/// <summary>
/// SQLite-backed implementation of <see cref="IUserFieldService"/>.
/// Field definitions are stored in AuthManager's internal database.
/// </summary>
internal sealed class UserFieldService : IUserFieldService
{
    private readonly IDbContextFactory<AuthManagerDbContext> _factory;

    public UserFieldService(IDbContextFactory<AuthManagerDbContext> factory)
        => _factory = factory;

    public async Task<List<UserFieldDefinition>> GetFieldDefinitionsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.UserFieldDefinitions
            .Where(f => f.IsVisible)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.DisplayName)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<List<UserFieldDefinition>> GetAllFieldDefinitionsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var rows = await db.UserFieldDefinitions
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.DisplayName)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<UserFieldDefinition?> GetFieldDefinitionAsync(
        string fieldId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.UserFieldDefinitions.FindAsync([fieldId], ct);
        return row is null ? null : Map(row);
    }

    public async Task SaveFieldAsync(UserFieldDefinition field, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(field.FieldId))
            field.FieldId = Slugify(field.DisplayName);

        await using var db  = await _factory.CreateDbContextAsync(ct);
        var existing        = await db.UserFieldDefinitions.FindAsync([field.FieldId], ct);

        if (existing is null)
        {
            // Assign sort order after current max
            var maxOrder = await db.UserFieldDefinitions
                .Select(f => (int?)f.SortOrder)
                .MaxAsync(ct) ?? -10;

            db.UserFieldDefinitions.Add(new UserFieldDefinitionRecord
            {
                FieldId       = field.FieldId,
                DisplayName   = field.DisplayName,
                FieldType     = field.FieldType.ToString(),
                IsRequired    = field.IsRequired,
                Placeholder   = field.Placeholder,
                DefaultValue  = field.DefaultValue,
                SelectOptions = field.SelectOptions,
                SortOrder     = maxOrder + 10,
                IsVisible     = field.IsVisible,
                HelpText      = field.HelpText,
                CreatedAt     = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            existing.DisplayName   = field.DisplayName;
            existing.FieldType     = field.FieldType.ToString();
            existing.IsRequired    = field.IsRequired;
            existing.Placeholder   = field.Placeholder;
            existing.DefaultValue  = field.DefaultValue;
            existing.SelectOptions = field.SelectOptions;
            existing.IsVisible     = field.IsVisible;
            existing.HelpText      = field.HelpText;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteFieldAsync(string fieldId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var row = await db.UserFieldDefinitions.FindAsync([fieldId], ct);
        if (row is not null)
        {
            db.UserFieldDefinitions.Remove(row);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ReorderFieldsAsync(
        IEnumerable<string> orderedFieldIds, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var ids   = orderedFieldIds.ToList();
        var rows  = await db.UserFieldDefinitions.ToListAsync(ct);
        var index = 0;

        foreach (var id in ids)
        {
            var row = rows.FirstOrDefault(r => r.FieldId == id);
            if (row is not null)
                row.SortOrder = index++ * 10;
        }

        await db.SaveChangesAsync(ct);
    }

    private static UserFieldDefinition Map(UserFieldDefinitionRecord r) => new()
    {
        FieldId       = r.FieldId,
        DisplayName   = r.DisplayName,
        FieldType     = Enum.TryParse<UserFieldType>(r.FieldType, out var t) ? t : UserFieldType.Text,
        IsRequired    = r.IsRequired,
        Placeholder   = r.Placeholder,
        DefaultValue  = r.DefaultValue,
        SelectOptions = r.SelectOptions,
        SortOrder     = r.SortOrder,
        IsVisible     = r.IsVisible,
        HelpText      = r.HelpText,
        CreatedAt     = r.CreatedAt,
    };

    private static string Slugify(string input) =>
        Regex.Replace(
            input.ToLowerInvariant().Trim().Replace(' ', '_'),
            @"[^a-z0-9_]", string.Empty);
}
