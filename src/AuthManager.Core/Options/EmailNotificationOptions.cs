namespace AuthManager.Core.Options;

/// <summary>
/// Email notification settings for auth events — verification, password reset,
/// welcome emails, and lockout alerts.
///
/// AuthManager uses the standard ASP.NET Core <c>IEmailSender</c> abstraction.
/// Bring your own sender (e.g. MailKit, SendGrid, AWS SES) or use the built-in
/// <c>SmtpEmailOptions</c> for direct SMTP.
/// </summary>
public sealed class EmailNotificationOptions
{
    /// <summary>Enable outbound email. Default: false.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Sender address shown on all outbound emails.</summary>
    public string FromAddress { get; set; } = "noreply@localhost";

    /// <summary>Sender display name.</summary>
    public string FromName { get; set; } = "Auth Manager";

    /// <summary>Base URL for links in emails (e.g. "https://app.example.com"). No trailing slash.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Send a welcome email when a new user is created by an admin.</summary>
    public bool SendWelcomeEmail { get; set; } = true;

    /// <summary>Automatically send an email-confirmation link when a new user is created.</summary>
    public bool SendEmailVerificationOnCreate { get; set; } = true;

    /// <summary>Notify a user by email when their account is locked out.</summary>
    public bool NotifyUserOnLockout { get; set; } = true;

    /// <summary>Customisable email subject/body templates.</summary>
    public EmailTemplates Templates { get; set; } = new();

    /// <summary>
    /// Optional built-in SMTP relay. Leave null to use a registered
    /// <c>IEmailSender</c> from the DI container instead.
    /// </summary>
    public SmtpOptions? Smtp { get; set; }
}

/// <summary>
/// Subject + body templates. Supports placeholders:
/// <c>{UserName}</c>, <c>{Email}</c>, <c>{AppName}</c>, <c>{Link}</c>, <c>{IpAddress}</c>.
/// </summary>
public sealed class EmailTemplates
{
    public string WelcomeSubject { get; set; } = "Welcome to {AppName}";
    public string WelcomeBody    { get; set; } =
        "Hi {UserName},\n\nYour account has been created. Sign in at {Link}.\n\nThanks,\nThe {AppName} Team";

    public string EmailVerificationSubject { get; set; } = "Confirm your email address";
    public string EmailVerificationBody    { get; set; } =
        "Hi {UserName},\n\nClick the link below to confirm your email:\n{Link}\n\nThe link expires in 24 hours.";

    public string PasswordResetSubject { get; set; } = "Reset your password";
    public string PasswordResetBody    { get; set; } =
        "Hi {UserName},\n\nClick the link below to reset your password:\n{Link}\n\nIf you did not request this, ignore this email.";

    public string LockoutSubject { get; set; } = "Your account has been locked";
    public string LockoutBody    { get; set; } =
        "Hi {UserName},\n\nYour account was locked after multiple failed login attempts. " +
        "Contact an administrator to unlock it, or wait until the lockout expires.\n\n" +
        "Sign-in IP: {IpAddress}";

    public string PasswordExpirySubject { get; set; } = "Your password is expiring soon";
    public string PasswordExpiryBody    { get; set; } =
        "Hi {UserName},\n\nYour password will expire in {DaysRemaining} days. " +
        "Change it now at {Link}.";
}

/// <summary>
/// Built-in SMTP configuration. Used only if <c>EmailNotificationOptions.Smtp</c> is set.
/// </summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
}
