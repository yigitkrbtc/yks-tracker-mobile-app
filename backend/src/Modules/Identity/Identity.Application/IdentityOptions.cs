namespace Nirengi.Modules.Identity.Application;

/// <summary>Identity policy values, bound from the "Identity" configuration section.</summary>
public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    /// <summary>Accounts registered with these addresses get the admin role (and must enrol TOTP).</summary>
    public IReadOnlyList<string> AdminEmails { get; set; } = [];

    /// <summary>Versions of the texts the client must have shown; older versions are refused at registration.</summary>
    public string TermsVersion { get; set; } = "1";

    public string PrivacyNoticeVersion { get; set; } = "1";

    public string MarketingConsentVersion { get; set; } = "1";

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan MfaTokenLifetime { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan CodeResendCooldown { get; set; } = TimeSpan.FromSeconds(60);

    public int MaxCodeAttempts { get; set; } = 5;

    public int LockoutThreshold { get; set; } = 10;

    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan DeletionGracePeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Unverified sign-ups are removed after this (privacy notice: 7 days).</summary>
    public TimeSpan UnverifiedRetention { get; set; } = TimeSpan.FromDays(7);
}
