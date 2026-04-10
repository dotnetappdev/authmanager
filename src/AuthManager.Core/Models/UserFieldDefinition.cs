namespace AuthManager.Core.Models;

/// <summary>
/// Defines a dynamic custom field that appears on every user record.
/// Field values are stored as <c>custom:fieldId</c> claims on the user
/// — no schema migration is needed to add or remove fields.
/// </summary>
public sealed class UserFieldDefinition
{
    /// <summary>
    /// Unique slug identifier used as the claim key suffix.
    /// E.g. <c>"department"</c> → claim type <c>"custom:department"</c>.
    /// Lower-case, no spaces.
    /// </summary>
    public string FieldId { get; set; } = string.Empty;

    /// <summary>Human-readable label shown in the user edit form.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The input type that controls how the field is rendered.</summary>
    public UserFieldType FieldType { get; set; } = UserFieldType.Text;

    /// <summary>Whether a non-empty value is required when saving the user.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Optional placeholder text shown inside the input.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Optional default value pre-filled for new users.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Pipe-separated options for <see cref="UserFieldType.Select"/> fields.
    /// E.g. <c>"Engineering|Marketing|Sales|HR"</c>
    /// </summary>
    public string? SelectOptions { get; set; }

    /// <summary>Sort order in the user edit form (ascending).</summary>
    public int SortOrder { get; set; }

    /// <summary>When false the field is hidden from the edit form but its data is preserved.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Optional descriptive text shown below the input.</summary>
    public string? HelpText { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Returns the options as a list, splitting on pipe character.</summary>
    public IReadOnlyList<string> GetSelectOptions() =>
        string.IsNullOrWhiteSpace(SelectOptions)
            ? []
            : SelectOptions.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The ASP.NET Identity claim type for this field's values.</summary>
    public string ClaimType => $"custom:{FieldId}";
}

/// <summary>Supported input types for dynamic user fields.</summary>
public enum UserFieldType
{
    /// <summary>Single-line text input.</summary>
    Text,

    /// <summary>Multi-line text area.</summary>
    TextArea,

    /// <summary>Email address input (validated on client).</summary>
    Email,

    /// <summary>Phone number input.</summary>
    Phone,

    /// <summary>URL input.</summary>
    Url,

    /// <summary>Numeric input (integer or decimal).</summary>
    Number,

    /// <summary>On/off toggle stored as "true" / "false".</summary>
    Boolean,

    /// <summary>Date picker — stored as ISO 8601 date string.</summary>
    Date,

    /// <summary>Date + time picker — stored as ISO 8601 datetime string.</summary>
    DateTime,

    /// <summary>Drop-down select with predefined options.</summary>
    Select,
}
