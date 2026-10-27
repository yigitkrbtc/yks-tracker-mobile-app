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
        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new TextPart("plain") { Text = message.Body };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(smtp.Host, smtp.Port, ToMailKit(smtp.Security), cancellationToken);
            if (!string.IsNullOrEmpty(smtp.Username))
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSendFailed(logger, ex);
        }
    }

    private static SecureSocketOptions ToMailKit(SmtpSecurity security) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls,
    };

    // The recipient is personal data; it stays out of the log.
    [LoggerMessage(Level = LogLevel.Error, Message = "Sending an identity e-mail failed")]
    private static partial void LogSendFailed(ILogger logger, Exception exception);
}
