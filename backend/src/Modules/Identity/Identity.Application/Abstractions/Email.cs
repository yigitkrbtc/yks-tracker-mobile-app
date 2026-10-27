namespace Nirengi.Modules.Identity.Application.Abstractions;

public sealed record EmailMessage(string To, string Subject, string Body);

public interface IEmailSender
{
    /// <summary>Best effort: failures are logged, not thrown; the user can ask for a new code.</summary>
    Task Send(EmailMessage message, CancellationToken cancellationToken);
}
