using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.Modules.Identity.Domain.Codes;

namespace Nirengi.Modules.Identity.Application.Abstractions;

public sealed record EmailMessage(string To, string Subject, string Body);

public interface IEmailSender
{
    /// <summary>
    /// Hands the mail to the relay. Throws when that fails (the Identity outbox retries with backoff, ADR-0021);
    /// only a failure that can never succeed, such as an address the relay refuses, is logged and swallowed.
    /// </summary>
    Task Send(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Identity's own outbox event for "mail this to the user" (ADR-0021, PR 2). The mail itself, which can hold a plain
/// code, travels only in <see cref="ProtectedPayload"/> (AES-GCM under a key derived from the HashKey); the outbox row keeps nothing readable.
/// </summary>
/// <param name="ValidUntil">After this instant the mail is pointless (the code expired) and is dropped unsent.</param>
public sealed record IdentityEmailRequested(Guid Id, DateTimeOffset OccurredAt, DateTimeOffset ValidUntil, string ProtectedPayload) : IIntegrationEvent
{
    /// <summary>
    /// What the payload is bound to: the event id and its deadline. A payload copied into another row (another id, a
    /// later <c>ValidUntil</c>) then fails to open instead of being mailed under the new row's terms.
    /// </summary>
    public static string AssociatedData(Guid id, DateTimeOffset validUntil) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{id:N}|{validUntil.UtcTicks}");
}

/// <summary>What <see cref="IdentityEmailRequested.ProtectedPayload"/> holds once unprotected.</summary>
public sealed record IdentityEmailPayload(EmailMessage Message, EmailCodeBinding? Code);

/// <summary>
/// Ties a mail to the code it carries. The mail is sent only while that code is still the live, unused one, so a
/// delayed mail cannot put a replaced code in the mailbox after the newer one.
/// </summary>
public sealed record EmailCodeBinding(Guid UserId, CodePurpose Purpose, string CodeHash);

/// <summary>Encrypts the mail payload while it waits in the outbox table.</summary>
public interface IEmailPayloadProtector
{
    /// <param name="plaintext">What to hide.</param>
    /// <param name="associatedData">Authenticated, not encrypted: opening needs the same value (see <see cref="IdentityEmailRequested.AssociatedData"/>).</param>
    string Protect(string plaintext, string associatedData);

    string Unprotect(string protectedValue, string associatedData);
}
