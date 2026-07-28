namespace AuthManager.Core.Options;

/// <summary>
/// SMS provider integration — lets OTP codes (and anything else the host app wires up via
/// <c>ISmsSenderService</c>) be delivered by text message through a real SMS gateway.
/// Configurable at runtime via /authmanager/otp; values set here are just the startup
/// defaults, same as <see cref="PaymentOptions"/>.
/// </summary>
public sealed class SmsOptions
{
    /// <summary>Master switch — when false, <c>ISmsSenderService.SendAsync</c> always fails
    /// regardless of which provider is configured below. Default: false.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Which configured provider actually sends messages. Default: None.</summary>
    public SmsProvider ActiveProvider { get; set; } = SmsProvider.None;

    /// <summary>Twilio — the largest global SMS/voice API provider.</summary>
    public TwilioSmsOptions Twilio { get; set; } = new();

    /// <summary>Vonage (formerly Nexmo) — global coverage, strong in the UK/EU.</summary>
    public VonageSmsOptions Vonage { get; set; } = new();

    /// <summary>MessageBird (Bird) — Europe-headquartered, strong UK/EU delivery routes.</summary>
    public MessageBirdSmsOptions MessageBird { get; set; } = new();

    /// <summary>Sinch — global CPaaS provider (absorbed MessageMedia, ex-mBlox routes).</summary>
    public SinchSmsOptions Sinch { get; set; } = new();

    /// <summary>Textlocal — UK-founded, one of the most widely used UK-specific SMS gateways.</summary>
    public TextlocalSmsOptions Textlocal { get; set; } = new();
}

/// <summary>Which SMS gateway <see cref="SmsOptions.ActiveProvider"/> selects.</summary>
public enum SmsProvider
{
    None,
    Twilio,
    Vonage,
    MessageBird,
    Sinch,
    Textlocal,
}

/// <summary>
/// Twilio credentials — from the Twilio Console (console.twilio.com). Uses the Messages
/// REST API (<c>POST /2010-04-01/Accounts/{AccountSid}/Messages.json</c>) with HTTP Basic auth.
/// </summary>
public sealed class TwilioSmsOptions
{
    /// <summary>Account SID (starts with "AC...").</summary>
    public string AccountSid { get; set; } = string.Empty;

    /// <summary>Auth Token — server-side only.</summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>Sending number in E.164 format (e.g. +14155552671), or a Messaging Service SID (starts with "MG...").</summary>
    public string FromNumber { get; set; } = string.Empty;
}

/// <summary>
/// Vonage (Nexmo) credentials — from the Vonage API Dashboard (dashboard.nexmo.com).
/// Uses the SMS API (<c>POST https://rest.nexmo.com/sms/json</c>).
/// </summary>
public sealed class VonageSmsOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>Sender ID/number shown to the recipient — an alphanumeric brand name (where supported) or an E.164 number.</summary>
    public string FromNumber { get; set; } = string.Empty;
}

/// <summary>
/// MessageBird (Bird) credentials — from the MessageBird Dashboard. Uses the REST API
/// (<c>POST https://rest.messagebird.com/messages</c>) with an Access Key.
/// </summary>
public sealed class MessageBirdSmsOptions
{
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Originator shown to the recipient — an alphanumeric sender ID or E.164 number.</summary>
    public string Originator { get; set; } = string.Empty;
}

/// <summary>
/// Sinch credentials — from the Sinch Dashboard. Uses the SMS API
/// (<c>POST https://sms.api.sinch.com/xms/v1/{ServicePlanId}/batches</c>) with a Bearer API token.
/// </summary>
public sealed class SinchSmsOptions
{
    public string ServicePlanId { get; set; } = string.Empty;
    public string ApiToken { get; set; } = string.Empty;
    public string FromNumber { get; set; } = string.Empty;
}

/// <summary>
/// Textlocal credentials — from the Textlocal Dashboard (control.textlocal.in for API key
/// generation). UK-founded, one of the most widely used SMS gateways for UK-only sending.
/// Uses the Send API (<c>POST https://api.txtlocal.com/send/</c>).
/// </summary>
public sealed class TextlocalSmsOptions
{
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Sender name shown to the recipient — up to 11 alphanumeric characters, or a validated UK long number.</summary>
    public string Sender { get; set; } = string.Empty;
}
