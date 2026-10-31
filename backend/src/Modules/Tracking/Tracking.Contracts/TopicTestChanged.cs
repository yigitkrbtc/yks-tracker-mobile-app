using Nirengi.BuildingBlocks.Application.Events;

namespace Nirengi.Modules.Tracking.Contracts;

/// <summary>
/// A topic test was recorded, edited or deleted. <paramref name="TopicIds"/> holds the topics before and after
/// the change, so a consumer (Analytics, S5) recomputes every topic the change touched. Handlers must be idempotent.
/// </summary>
public sealed record TopicTestChanged(Guid Id, DateTimeOffset OccurredAt, Guid UserId, Guid TopicTestId, IReadOnlyList<Guid> TopicIds)
    : IIntegrationEvent;
