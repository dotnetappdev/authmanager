namespace AuthManager.Core.Options;

/// <summary>
/// Schema-driven custom user attributes — equivalent to Keycloak's "User Attributes" tab.
///
/// Define attribute schemas here; AuthManager stores the values as user claims
/// with a <c>custom:</c> prefix (e.g. <c>custom:department</c>).
///
/// The UI renders an appropriate input control for each attribute type and
/// validates required fields automatically.
/// </summary>
public sealed class UserAttributeOptions
{
    /// <summary>
    /// Attribute schema definitions. Order determines display order in the UI.
    /// </summary>
    public List<UserAttributeSchema> Attributes { get; set; } = [];

    /// <summary>
    /// Claim prefix used when storing custom attributes as identity claims.
    /// Default: "custom:".
    /// </summary>
    public string ClaimPrefix { get; set; } = "custom:";

    /// <summary>
    /// Show the custom attributes tab on the user detail page. Default: true.
    /// Set false to hide the tab entirely if you have no attributes defined.
    /// </summary>
    public bool ShowAttributeTab { get; set; } = true;
}

/// <summary>
/// Schema definition for a single custom user attribute.
/// </summary>
public sealed class UserAttributeSchema
{
    /// <summary>
    /// Machine-readable key. Used as the claim type suffix after the prefix.
    /// Must be URL-safe (letters, digits, dashes, underscores). E.g. "department".
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Human-readable label shown in the UI. E.g. "Department".</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Helper text shown under the input. Optional.</summary>
    public string? Description { get; set; }

    /// <summary>Input type that determines the UI control and validation.</summary>
    public UserAttributeType Type { get; set; } = UserAttributeType.Text;

    /// <summary>Whether this attribute must have a value. Default: false.</summary>
    public bool Required { get; set; } = false;

    /// <summary>Whether the attribute is visible to end-users in their profile. Default: true.</summary>
    public bool UserVisible { get; set; } = true;

    /// <summary>Whether the attribute is editable by end-users. Default: false (admin-only).</summary>
    public bool UserEditable { get; set; } = false;

    /// <summary>
    /// For <see cref="UserAttributeType.Select"/>, the allowed values.
    /// </summary>
    public List<string> AllowedValues { get; set; } = [];

    /// <summary>Default value applied when a new user is created without this attribute.</summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// For <see cref="UserAttributeType.Text"/>, the maximum character length.
    /// 0 = unlimited.
    /// </summary>
    public int MaxLength { get; set; } = 0;

    /// <summary>
    /// Optional regex validation pattern.
    /// E.g. <c>@"^[A-Z]{2,3}-\d{4}$"</c> for an employee ID.
    /// </summary>
    public string? ValidationPattern { get; set; }

    /// <summary>Error message shown when <see cref="ValidationPattern"/> fails.</summary>
    public string? ValidationMessage { get; set; }
}

/// <summary>
/// The data type / input control for a custom user attribute.
/// </summary>
public enum UserAttributeType
{
    /// <summary>Single-line free text.</summary>
    Text,

    /// <summary>Multi-line free text (textarea).</summary>
    TextArea,

    /// <summary>Integer number.</summary>
    Number,

    /// <summary>True / false toggle.</summary>
    Boolean,

    /// <summary>Date picker (stored as ISO 8601 date string).</summary>
    Date,

    /// <summary>Date + time picker (stored as ISO 8601 datetime).</summary>
    DateTime,

    /// <summary>Dropdown with pre-defined <see cref="UserAttributeSchema.AllowedValues"/>.</summary>
    Select,

    /// <summary>Multi-select checkbox list (stored as comma-separated values).</summary>
    MultiSelect,

    /// <summary>URL input with format validation.</summary>
    Url,

    /// <summary>Email address input with format validation.</summary>
    Email,

    /// <summary>Phone number input.</summary>
    Phone,

    /// <summary>Masked / sensitive text (e.g. internal reference IDs).</summary>
    Secret,
}
