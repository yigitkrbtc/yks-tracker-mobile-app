using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

    private const string Address = "ayse@ornek.com";

    [Fact]
    public async Task A_temporary_rejection_leaves_without_the_servers_text_or_the_address()
    {
        var logger = new CapturingLogger();
        var failure = new SmtpCommandException(
            SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxBusy, $"450 4.2.0 <{Address}>: Recipient address rejected: Greylisted", new IOException($"inner {Address}"));

        var thrown = await Should.ThrowAsync<SmtpRelayException>(() => Sender(logger).Deliver(() => throw failure));

        thrown.Message.ShouldBe("SMTP RecipientNotAccepted 450");
        thrown.InnerException.ShouldBeNull();
        thrown.ToString().ShouldNotContain(Address);
        logger.Output.ShouldNotContain(Address);
    }

    [Fact]
    public async Task Other_failures_are_sanitized_to_their_kind()
    {
        var thrown = await Should.ThrowAsync<SmtpRelayException>(() =>
            Sender(new CapturingLogger()).Deliver(() => throw new SmtpProtocolException($"unexpected reply for {Address}")));

        thrown.Message.ShouldBe("SMTP delivery failed (SmtpProtocolException)");
        thrown.ToString().ShouldNotContain(Address);
    }

    [Fact]
    public async Task A_cancellation_passes_through_untouched() =>
        await Should.ThrowAsync<OperationCanceledException>(() => Sender(new CapturingLogger()).Deliver(() => throw new OperationCanceledException()));

    [Fact]
    public async Task A_permanent_refusal_is_swallowed_and_logged_without_the_address()
    {
        var logger = new CapturingLogger();
        var refused = new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, $"550 5.1.1 <{Address}>: user unknown");

        await Sender(logger).Deliver(() => throw refused);

        logger.Output.ShouldContain("550");
        logger.Output.ShouldNotContain(Address);
        logger.Output.ShouldNotContain("user unknown");
    }

    [Fact]
    public async Task A_parse_failure_is_swallowed_and_logged_without_the_address()
    {
        var logger = new CapturingLogger();

        await Sender(logger).Deliver(() => throw new ParseException($"Invalid addr-spec: {Address}", 0, 0));

        logger.Output.ShouldNotBeEmpty();
        logger.Output.ShouldNotContain(Address);
    }

    private static SmtpEmailSender Sender(ILogger<SmtpEmailSender> logger) => new(Options.Create(new SmtpOptions()), logger);

    private sealed class CapturingLogger : ILogger<SmtpEmailSender>
    {
        private readonly List<string> _lines = [];

        public string Output => string.Join('\n', _lines);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _lines.Add($"{formatter(state, exception)} {exception}");
    }
}
