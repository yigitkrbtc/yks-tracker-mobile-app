using System.ComponentModel.DataAnnotations;

namespace Nirengi.Modules.Identity.Infrastructure.Mail;

public enum SmtpSecurity
{
    None = 0,
    StartTls = 1,
    SslOnConnect = 2,
}

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

    public string? Username { get; set; }

    public string? Password { get; set; }

    [Required]
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Nirengi";
}
