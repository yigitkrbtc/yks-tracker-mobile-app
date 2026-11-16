namespace Nirengi.Modules.Identity.Infrastructure.Mail;

/// <summary>
/// A failed delivery, stripped of what the relay or the client library said: those texts can quote the recipient
/// ("450 4.2.0 &lt;ayse@ornek.com&gt;: Recipient address rejected"), and this message ends up in the outbox's last_error and the log.
/// </summary>
internal sealed class SmtpRelayException : Exception
{
    public SmtpRelayException()
    {
    }

    public SmtpRelayException(string message)
        : base(message)
    {
    }

    public SmtpRelayException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
