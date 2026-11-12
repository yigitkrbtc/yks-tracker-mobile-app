using Nirengi.Modules.Identity.Domain.Users;

namespace Nirengi.Modules.Identity.Application;

/// <summary>Identity policy values, bound from the "Identity" configuration section.</summary>
public sealed class IdentityOptions
{
    public const string SectionName = "Identity";

    /// <summary>
    /// Accounts with these addresses are admins (and must enrol TOTP). Read at every sign-in, so removing an address
    /// demotes the account at its next sign-in or refresh (ADR-0021).
    /// </summary>
    public IReadOnlyList<string> AdminEmails { get; set; } = [];

    /// <summary>Versions of the texts the client must have shown; older versions are refused at registration.</summary>
    public string TermsVersion { get; set; } = "1";

    public string PrivacyNoticeVersion { get; set; } = "1";

    public string MarketingConsentVersion { get; set; } = "1";

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Admin sessions end sooner: the second factor is asked again at least this often (ADR-0021).</summary>
    public TimeSpan AdminRefreshTokenLifetime { get; set; } = TimeSpan.FromHours(12);

    /// <summary>
    /// A rotated refresh token presented again this soon is a lost response being retried, not theft: the caller gets
    /// the successor's rotation instead of losing the session (ADR-0021).
    /// </summary>
    public TimeSpan RefreshReuseGrace { get; set; } = TimeSpan.FromSeconds(60);

    public TimeSpan MfaTokenLifetime { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan CodeResendCooldown { get; set; } = TimeSpan.FromSeconds(60);

    public int MaxCodeAttempts { get; set; } = 5;

    public int LockoutThreshold { get; set; } = 10;

    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan DeletionGracePeriod { get; set; } = TimeSpan.FromDays(30);

    /// <summary>Unverified sign-ups are removed after this (privacy notice: 7 days).</summary>
    public TimeSpan UnverifiedRetention { get; set; } = TimeSpan.FromDays(7);

    public UserRole RoleFor(string normalizedEmail) =>
        AdminEmails.Any(a => Email.Normalize(a) == normalizedEmail) ? UserRole.Admin : UserRole.Student;
}
