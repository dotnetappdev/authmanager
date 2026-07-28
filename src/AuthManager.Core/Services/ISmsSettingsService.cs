using AuthManager.Core.Options;

namespace AuthManager.Core.Services;

/// <summary>
/// Reads and updates SMS provider settings at runtime — equivalent to
/// <see cref="IPaymentSettingsService"/> but for Twilio/Vonage/MessageBird/Sinch/Textlocal.
///
/// The default implementation persists overrides to the AuthManager settings store so
/// they survive application restarts; secret fields already saved are never round-tripped
/// back out (see <see cref="SmsSettingsInfo"/>) — only newly submitted values overwrite them.
/// </summary>
public interface ISmsSettingsService
{
    /// <summary>Get the currently effective SMS settings, with secrets masked.</summary>
    Task<SmsSettingsInfo> GetSettingsAsync(CancellationToken ct = default);

    /// <summary>Persist updated SMS settings. Blank secret fields leave the previously saved secret untouched.</summary>
    Task UpdateSettingsAsync(UpdateSmsSettingsDto dto, CancellationToken ct = default);

    /// <summary>
    /// Get the currently effective SMS settings with secrets in the clear — for
    /// server-side use only (<c>ISmsSenderService</c> calling out to the active provider).
    /// Never expose this over an API surface; use <see cref="GetSettingsAsync"/> for that.
    /// </summary>
    Task<SmsOptions> GetRawSettingsAsync(CancellationToken ct = default);
}

/// <summary>SMS settings as returned to the UI — secret fields are masked, never echoed in full.</summary>
public sealed class SmsSettingsInfo
{
    public bool Enabled { get; set; }
    public SmsProvider ActiveProvider { get; set; } = SmsProvider.None;

    public string TwilioAccountSid { get; set; } = string.Empty;
    public bool TwilioAuthTokenSet { get; set; }
    public string TwilioAuthTokenMasked { get; set; } = string.Empty;
    public string TwilioFromNumber { get; set; } = string.Empty;

    public string VonageApiKey { get; set; } = string.Empty;
    public bool VonageApiSecretSet { get; set; }
    public string VonageApiSecretMasked { get; set; } = string.Empty;
    public string VonageFromNumber { get; set; } = string.Empty;

    public bool MessageBirdApiKeySet { get; set; }
    public string MessageBirdApiKeyMasked { get; set; } = string.Empty;
    public string MessageBirdOriginator { get; set; } = string.Empty;

    public string SinchServicePlanId { get; set; } = string.Empty;
    public bool SinchApiTokenSet { get; set; }
    public string SinchApiTokenMasked { get; set; } = string.Empty;
    public string SinchFromNumber { get; set; } = string.Empty;

    public bool TextlocalApiKeySet { get; set; }
    public string TextlocalApiKeyMasked { get; set; } = string.Empty;
    public string TextlocalSender { get; set; } = string.Empty;
}

/// <summary>
/// Update payload for SMS settings. Secret fields (<see cref="TwilioAuthToken"/>,
/// <see cref="VonageApiSecret"/>, <see cref="MessageBirdApiKey"/>, <see cref="SinchApiToken"/>,
/// <see cref="TextlocalApiKey"/>) are only overwritten when non-null/non-empty — leave them
/// blank to keep the previously saved value.
/// </summary>
public sealed class UpdateSmsSettingsDto
{
    public bool Enabled { get; set; }
    public SmsProvider ActiveProvider { get; set; } = SmsProvider.None;

    public string TwilioAccountSid { get; set; } = string.Empty;
    public string? TwilioAuthToken { get; set; }
    public string TwilioFromNumber { get; set; } = string.Empty;

    public string VonageApiKey { get; set; } = string.Empty;
    public string? VonageApiSecret { get; set; }
    public string VonageFromNumber { get; set; } = string.Empty;

    public string? MessageBirdApiKey { get; set; }
    public string MessageBirdOriginator { get; set; } = string.Empty;

    public string SinchServicePlanId { get; set; } = string.Empty;
    public string? SinchApiToken { get; set; }
    public string SinchFromNumber { get; set; } = string.Empty;

    public string? TextlocalApiKey { get; set; }
    public string TextlocalSender { get; set; } = string.Empty;
}
