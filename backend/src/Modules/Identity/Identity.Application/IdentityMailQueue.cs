using System.Text.Json;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Application.SignIn;

namespace Nirengi.Modules.Identity.Application;

/// <summary>
/// Queues a mail in Identity's outbox, so it commits with the change that caused it and is sent (and retried) by the
/// outbox worker instead of inside the request. Call before the unit of work saves.
/// </summary>
internal sealed class IdentityMailQueue(IIdentityUnitOfWork unitOfWork, IEmailPayloadProtector protector, IClock clock)
{
    /// <summary>How long a mail without a code stays worth sending.</summary>
    public static readonly TimeSpan NoticeValidity = TimeSpan.FromHours(24);

    public void Enqueue(EmailMessage message, TimeSpan validFor, EmailCodeBinding? code = null)
    {
        var now = clock.UtcNow;
        var id = Guid.CreateVersion7(now);
        var validUntil = now + validFor;
        var payload = protector.Protect(
            JsonSerializer.Serialize(new IdentityEmailPayload(message, code)),
            IdentityEmailRequested.AssociatedData(id, validUntil));
        unitOfWork.Enqueue(new IdentityEmailRequested(id, now, validUntil, payload));
    }

    public void EnqueueCode(EmailMessage message, IssuedCode issued, TimeSpan validFor) => Enqueue(message, validFor, issued.Binding);
}
