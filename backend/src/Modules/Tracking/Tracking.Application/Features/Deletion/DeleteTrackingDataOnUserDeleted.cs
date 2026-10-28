using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.Modules.Identity.Contracts;
using Nirengi.Modules.Tracking.Application.Abstractions;

namespace Nirengi.Modules.Tracking.Application.Features.Deletion;

/// <summary>Removes everything Tracking holds about a purged account (ADR-0004). Safe to run twice.</summary>
internal sealed class DeleteTrackingDataOnUserDeleted(IStudyProfileRepository profiles) : IIntegrationEventHandler<UserDeleted>
{
    public Task Handle(UserDeleted integrationEvent, CancellationToken cancellationToken) =>
        profiles.DeleteForUser(integrationEvent.UserId, cancellationToken);
}
