using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Contracts;
using Nirengi.Modules.Tracking.Domain;

namespace Nirengi.Modules.Tracking.Application.Features.TopicTests;

/// <summary>
/// Soft-deletes a test (a tombstone stays for other devices). Idempotent: an unknown or already deleted id, or a
/// delete older than the stored edit, succeeds without changing anything.
/// </summary>
public sealed record DeleteTopicTestCommand(Guid UserId, Guid Id, DateTimeOffset ModifiedAt) : ICommand<Unit>;

internal sealed class DeleteTopicTestHandler(ITopicTestRepository tests, ITrackingUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<DeleteTopicTestCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteTopicTestCommand command, CancellationToken cancellationToken)
    {
        var test = await tests.Get(command.Id, cancellationToken);
        if (test is null)
        {
            return Unit.Value;
        }

        if (test.UserId != command.UserId)
        {
            return TrackingErrors.TopicTestNotFound;
        }

        var now = clock.UtcNow;
        var topics = test.Results.Select(r => r.TopicId).ToList();
        if (!test.Delete(command.ModifiedAt, now))
        {
            return Unit.Value;
        }

        unitOfWork.Enqueue(new TopicTestChanged(Guid.CreateVersion7(now), now, command.UserId, test.Id, topics));
        return await unitOfWork.TrySaveChanges(cancellationToken) ? Unit.Value : TrackingErrors.TopicTestConflict;
    }
}
