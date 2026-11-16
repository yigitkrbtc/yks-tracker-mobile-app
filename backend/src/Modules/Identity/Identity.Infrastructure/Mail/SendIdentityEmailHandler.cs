using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.Modules.Identity.Application.Abstractions;
using Nirengi.Modules.Identity.Infrastructure.Persistence.Configurations;

namespace Nirengi.Modules.Identity.Infrastructure.Mail;

/// <summary>
/// Sends the mail an <see cref="IdentityEmailRequested"/> asks for. A failing relay throws, so the outbox retries with
/// backoff. Delivery is at least once, so the handler is idempotent: a mail that was sent is recorded (separate commit,
/// the outbox row is locked by the worker) and a redelivery is skipped. A crash between sending and recording can still
/// mail twice; with a code in the mail, both copies are the same code.
/// </summary>
internal sealed partial class SendIdentityEmailHandler(
    IdentityDbContext db,
    IVerificationCodeRepository codes,
    IEmailPayloadProtector protector,
    IEmailSender sender,
    IClock clock,
    ILogger<SendIdentityEmailHandler> logger) : IIntegrationEventHandler<IdentityEmailRequested>
{
    /// <summary>Records only need to outlive the longest <c>ValidUntil</c> (a day); a redelivery after that is dropped as expired.</summary>
    private static readonly TimeSpan RecordRetention = TimeSpan.FromDays(7);

    public async Task Handle(IdentityEmailRequested integrationEvent, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        if (now > integrationEvent.ValidUntil)
        {
            LogExpired(logger, integrationEvent.Id);
            return;
        }

        if (await db.SentEmails.AnyAsync(e => e.EventId == integrationEvent.Id, cancellationToken))
        {
            return;
        }

        IdentityEmailPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<IdentityEmailPayload>(protector.Unprotect(
                integrationEvent.ProtectedPayload,
                IdentityEmailRequested.AssociatedData(integrationEvent.Id, integrationEvent.ValidUntil)));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            // The key ring or the payload is damaged; retrying cannot fix that, and the user can ask for a new code.
            LogUnreadable(logger, integrationEvent.Id, ex);
            return;
        }

        if (payload is null)
        {
            LogUnreadable(logger, integrationEvent.Id, null);
            return;
        }

        if (payload.Code is { } binding && !await IsLiveCode(binding, cancellationToken))
        {
            LogSuperseded(logger, integrationEvent.Id);
            return;
        }

        await sender.Send(payload.Message, cancellationToken);

        db.SentEmails.Add(new SentEmail { EventId = integrationEvent.Id, SentAt = now });
        await db.SaveChangesAsync(cancellationToken);
        await db.SentEmails.Where(e => e.SentAt < now - RecordRetention).ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<bool> IsLiveCode(EmailCodeBinding binding, CancellationToken cancellationToken)
    {
        var live = await codes.Get(binding.UserId, binding.Purpose, cancellationToken);
        return live is not null && live.ConsumedAt is null && live.CodeHash == binding.CodeHash;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Identity e-mail {EventId} expired before it could be sent; dropped")]
    private static partial void LogExpired(ILogger logger, Guid eventId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Identity e-mail {EventId} carries a code that is no longer the live one; dropped")]
    private static partial void LogSuperseded(ILogger logger, Guid eventId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Identity e-mail {EventId} payload cannot be read; dropped")]
    private static partial void LogUnreadable(ILogger logger, Guid eventId, Exception? exception);
}
