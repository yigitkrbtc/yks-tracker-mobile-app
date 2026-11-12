using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Nirengi.Modules.Identity.Application.Abstractions;

namespace Nirengi.Modules.Identity.Infrastructure.Mail;

internal sealed partial class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task Send(EmailMessage message, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        try
        {
            // An address our looser check accepts can still fail MimeKit's parser; no retry will change that.
            using var mime = new MimeMessage();
            mime.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
            mime.To.Add(MailboxAddress.Parse(message.To));
            mime.Subject = message.Subject;
            mime.Body = new TextPart("plain") { Text = message.Body };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtp.Host, smtp.Port, ToMailKit(smtp.Security), cancellationToken);
            if (!string.IsNullOrEmpty(smtp.Username))
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
        catch (Exception ex) when (IsPermanentRefusal(ex))
        {
            // The relay or the parser refuses this recipient for good: retrying would only repeat it forever.
            LogRecipientRefused(logger, ex);
        }
    }

    /// <summary>
    /// True only for a refusal no retry can change: an address the parser rejects, or a recipient the relay rejects with a
    /// permanent (5xx) status. A temporary (4xx) rejection, such as greylisting or a full mailbox, is retried.
    /// </summary>
    internal static bool IsPermanentRefusal(Exception exception) => exception is ParseException
        or SmtpCommandException { ErrorCode: SmtpErrorCode.RecipientNotAccepted, StatusCode: >= (SmtpStatusCode)500 };

    private static SecureSocketOptions ToMailKit(SmtpSecurity security) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls,
    };

    // The recipient is personal data; it stays out of the log. Every other failure propagates: the outbox retries it.
    [LoggerMessage(Level = LogLevel.Warning, Message = "An identity e-mail was dropped: the recipient is refused")]
    private static partial void LogRecipientRefused(ILogger logger, Exception exception);
}
