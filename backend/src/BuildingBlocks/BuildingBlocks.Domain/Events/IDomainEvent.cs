namespace Nirengi.BuildingBlocks.Domain.Events;

/// <summary>Something that happened inside one module's aggregate.</summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
