using System.Diagnostics.CodeAnalysis;

namespace Nirengi.BuildingBlocks.Application.Events;

/// <summary>
/// A fact one module publishes for others. Declared in the publisher's Contracts project,
/// written to the publisher's outbox in the same transaction as the change (ADR-0002).
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }

    DateTimeOffset OccurredAt { get; }
}

[SuppressMessage("Naming", "CA1711", Justification = "Handler of an integration event, not a .NET event delegate.")]
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task Handle(TEvent integrationEvent, CancellationToken cancellationToken);
}

/// <summary>Queues an integration event; it is persisted with the module's next SaveChanges.</summary>
public interface IEventOutbox
{
    void Enqueue(IIntegrationEvent integrationEvent);
}
