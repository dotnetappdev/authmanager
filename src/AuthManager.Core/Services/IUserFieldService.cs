using AuthManager.Core.Models;

namespace AuthManager.Core.Services;

/// <summary>
/// Manages dynamic custom field definitions for user records.
/// Field values are stored as <c>custom:fieldId</c> claims on the user;
/// definitions are stored in AuthManager's internal database.
/// </summary>
public interface IUserFieldService
{
    /// <summary>Returns all visible field definitions ordered by <see cref="UserFieldDefinition.SortOrder"/>.</summary>
    Task<List<UserFieldDefinition>> GetFieldDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Returns all field definitions including hidden ones (for admin management).</summary>
    Task<List<UserFieldDefinition>> GetAllFieldDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Returns a single field definition by its ID, or null if not found.</summary>
    Task<UserFieldDefinition?> GetFieldDefinitionAsync(string fieldId, CancellationToken ct = default);

    /// <summary>Creates or updates a field definition. Generates a slug ID if empty.</summary>
    Task SaveFieldAsync(UserFieldDefinition field, CancellationToken ct = default);

    /// <summary>Permanently removes a field definition. Existing claim values on users are NOT deleted.</summary>
    Task DeleteFieldAsync(string fieldId, CancellationToken ct = default);

    /// <summary>
    /// Reorders fields. Pass the full ordered list of field IDs;
    /// SortOrder is reassigned as 0, 10, 20 … to leave gaps for future inserts.
    /// </summary>
    Task ReorderFieldsAsync(IEnumerable<string> orderedFieldIds, CancellationToken ct = default);
}

/// <summary>
/// Manages the display names used throughout the AuthManager UI
/// (e.g. "User" → "Member", "Users" → "Members").
/// </summary>
public interface IEntityNamingService
{
    /// <summary>Singular display name for the user entity. Default: "User".</summary>
    string GetUserDisplayName();

    /// <summary>Plural display name for the user entity. Default: "Users".</summary>
    string GetUsersDisplayName();

    /// <summary>Persists updated naming preferences.</summary>
    Task SaveNamingAsync(string singular, string plural, CancellationToken ct = default);
}
