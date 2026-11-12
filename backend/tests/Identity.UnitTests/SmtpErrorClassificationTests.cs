using MailKit.Net.Smtp;
using MimeKit;
using Nirengi.Modules.Identity.Infrastructure.Mail;
using Shouldly;

namespace Nirengi.Identity.UnitTests;

/// <summary>Only a refusal no retry can change is dropped; everything else must reach the outbox's retry.</summary>
public sealed class SmtpErrorClassificationTests
{
    [Theory]
    [InlineData(SmtpStatusCode.MailboxUnavailable)]
    [InlineData(SmtpStatusCode.UserNotLocalTryAlternatePath)]
    [InlineData(SmtpStatusCode.MailboxNameNotAllowed)]
    public void A_permanent_recipient_rejection_is_dropped(SmtpStatusCode status) =>
        SmtpEmailSender.IsPermanentRefusal(new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, status, "no")).ShouldBeTrue();

    [Theory]
    [InlineData(SmtpStatusCode.MailboxBusy)]
    [InlineData(SmtpStatusCode.InsufficientStorage)]
    [InlineData(SmtpStatusCode.ServiceNotAvailable)]
    public void A_temporary_recipient_rejection_is_retried(SmtpStatusCode status) =>
        SmtpEmailSender.IsPermanentRefusal(new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, status, "later")).ShouldBeFalse();

    [Fact]
    public void An_address_the_parser_rejects_is_dropped() =>
        SmtpEmailSender.IsPermanentRefusal(new ParseException("bad address", 0, 0)).ShouldBeTrue();

    [Fact]
    public void Other_failures_are_retried()
    {
        SmtpEmailSender.IsPermanentRefusal(new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, SmtpStatusCode.TransactionFailed, "x")).ShouldBeFalse();
        SmtpEmailSender.IsPermanentRefusal(new IOException("connection reset")).ShouldBeFalse();
        SmtpEmailSender.IsPermanentRefusal(new OperationCanceledException()).ShouldBeFalse();
    }
}
