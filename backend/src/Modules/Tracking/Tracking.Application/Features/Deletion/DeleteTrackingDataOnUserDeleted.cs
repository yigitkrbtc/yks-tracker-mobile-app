using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.Modules.Identity.Contracts;
using Nirengi.Modules.Tracking.Application.Abstractions;

namespace Nirengi.Modules.Tracking.Application.Features.Deletion;

/// <summary>Removes everything Tracking holds about a purged account (ADR-0004). Safe to run twice.</summary>
internal sealed class DeleteTrackingDataOnUserDeleted(
    IStudyProfileRepository profiles,
    ITopicTestRepository topicTests,
    IMockExamRepository mockExams,
    ITopicProgressRepository progress)
    : IIntegrationEventHandler<UserDeleted>
{
    public async Task Handle(UserDeleted integrationEvent, CancellationToken cancellationToken)
    {
        await topicTests.DeleteForUser(integrationEvent.UserId, cancellationToken);
        await mockExams.DeleteForUser(integrationEvent.UserId, cancellationToken);
        await progress.DeleteForUser(integrationEvent.UserId, cancellationToken);
        await profiles.DeleteForUser(integrationEvent.UserId, cancellationToken);
    }
}
