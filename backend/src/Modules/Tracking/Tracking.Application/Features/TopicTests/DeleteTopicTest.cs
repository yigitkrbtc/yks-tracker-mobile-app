using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Contracts;
using Nirengi.Modules.Tracking.Domain;

namespace Nirengi.Modules.Tracking.Application.Features.TopicTests;

/// <summary>
/// Soft-deletes a test (a tombstone stays for other devices). Idempotent: an unknown or already deleted id
/// succeeds without changing anything (null). A delete older than the stored edit loses: the stored test comes
/// back, as with a stale PUT, so the client restores it instead of dropping its copy.
/// </summary>
public sealed record DeleteTopicTestCommand(Guid UserId, Guid Id, DateTimeOffset ModifiedAt) : ICommand<TopicTestResponse?>;

internal sealed class DeleteTopicTestHandler(ITopicTestRepository tests, ITrackingUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<DeleteTopicTestCommand, TopicTestResponse?>
{
    public async Task<Result<TopicTestResponse?>> Handle(DeleteTopicTestCommand command, CancellationToken cancellationToken)
    {
        var test = await tests.Get(command.Id, cancellationToken);
        if (test is null)
        {
            return (TopicTestResponse?)null;
        }

        if (test.UserId != command.UserId)
        {
            return TrackingErrors.TopicTestNotFound;
        }

        var now = clock.UtcNow;
        var topics = test.Results.Select(r => r.TopicId).ToList();
        var wasDeleted = test.IsDeleted;
        if (!test.Delete(command.ModifiedAt, now))
        {
            return wasDeleted ? null : TopicTestResponse.From(test);
        }

        unitOfWork.Enqueue(new TopicTestChanged(Guid.CreateVersion7(now), now, command.UserId, test.Id, topics));
        return await unitOfWork.TrySaveChanges(cancellationToken) ? (TopicTestResponse?)null : TrackingErrors.TopicTestConflict;
    }
}
