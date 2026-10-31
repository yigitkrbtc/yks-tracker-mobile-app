using Nirengi.BuildingBlocks.Application.Events;

namespace Nirengi.Modules.Tracking.Contracts;

/// <summary>
/// A mock exam was recorded, edited (marks included) or deleted. <paramref name="SectionCodes"/> and
/// <paramref name="TopicIds"/> hold the sections and marked topics before and after the change, so a consumer
/// (Analytics, S5) recomputes everything the change touched. Handlers must be idempotent.
/// </summary>
public sealed record MockExamChanged(
    Guid Id,
    DateTimeOffset OccurredAt,
    Guid UserId,
    Guid MockExamId,
    IReadOnlyList<string> SectionCodes,
    IReadOnlyList<Guid> TopicIds) : IIntegrationEvent;
