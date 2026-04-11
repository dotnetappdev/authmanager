namespace AuthManager.Core.Models;

/// <summary>
/// Data collected by the first-run setup wizard.
/// </summary>
public sealed class SetupWizardDto
{
    // Step 1 – Welcome
    public string AppTitle { get; set; } = "Auth Manager";

    // Step 2 – Hosting / Database
    /// <summary>SQLite (default), SqlServer</summary>
    public string DatabaseProvider   { get; set; } = "SQLite";
    public string ConnectionString   { get; set; } = "Data Source=authmanager.db";
    /// <summary>Hosting environment hint — used to show relevant docs.</summary>
    public string HostingEnvironment { get; set; } = "SelfHosted";  // SelfHosted | Azure | AWS | Docker

    // Step 3 – Admin Account
    public string AdminEmail    { get; set; } = "";
    public string AdminUserName { get; set; } = "";
    public string AdminPassword { get; set; } = "";
    public string AdminPasswordConfirm { get; set; } = "";

    // Step 4 – Password Policy
    public int  MinPasswordLength   { get; set; } = 8;
    public bool RequireUppercase    { get; set; } = true;
    public bool RequireLowercase    { get; set; } = true;
    public bool RequireDigit        { get; set; } = true;
    public bool RequireSpecialChar  { get; set; } = false;

    // Step 4 – Brute-force protection
    public bool EnableBruteForce        { get; set; } = true;
    public int  MaxFailedAttempts       { get; set; } = 5;
    public int  LockoutDurationMinutes  { get; set; } = 15;

    // Step 5 – Authentication extras
    public bool Require2FA        { get; set; } = false;
    public bool EnableGoogleOAuth { get; set; } = false;
    public string GoogleClientId     { get; set; } = "";
    public string GoogleClientSecret { get; set; } = "";
    public bool EnableMicrosoftOAuth  { get; set; } = false;
    public string MicrosoftClientId     { get; set; } = "";
    public string MicrosoftClientSecret { get; set; } = "";
    public string MicrosoftTenantId     { get; set; } = "common";
    public bool EnableGitHubOAuth  { get; set; } = false;
    public string GitHubClientId     { get; set; } = "";
    public string GitHubClientSecret { get; set; } = "";
}
