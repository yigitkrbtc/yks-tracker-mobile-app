using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.Modules.Analytics.Application.Abstractions;
using Nirengi.Modules.Identity.Contracts;
using Nirengi.Modules.Tracking.Contracts;

namespace Nirengi.Modules.Analytics.Application.Features;

/// <summary>
/// Recomputes the student's analysis after a record change, so the next read (and S6 recommendations) find it
/// current. Idempotent: an unchanged stamp is a no-op, a replayed event recomputes the same result.
/// </summary>
internal sealed class RefreshAnalysisOnTrackingChange(IAnalysisRefresher refresher)
    : IIntegrationEventHandler<TopicTestChanged>, IIntegrationEventHandler<MockExamChanged>
{
    public Task Handle(TopicTestChanged integrationEvent, CancellationToken cancellationToken) =>
        refresher.EnsureFresh(integrationEvent.UserId, cancellationToken);

    public Task Handle(MockExamChanged integrationEvent, CancellationToken cancellationToken) =>
        refresher.EnsureFresh(integrationEvent.UserId, cancellationToken);
}

/// <summary>Removes a purged account's analysis (ADR-0004). Safe to run twice.</summary>
internal sealed class DeleteAnalyticsOnUserDeleted(IAnalyticsStore store) : IIntegrationEventHandler<UserDeleted>
{
    public Task Handle(UserDeleted integrationEvent, CancellationToken cancellationToken) =>
        store.DeleteForUser(integrationEvent.UserId, cancellationToken);
}
