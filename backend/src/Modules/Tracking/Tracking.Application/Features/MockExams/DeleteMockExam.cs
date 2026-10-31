using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Contracts;
using Nirengi.Modules.Tracking.Domain;

namespace Nirengi.Modules.Tracking.Application.Features.MockExams;

/// <summary>
/// Soft-deletes a mock exam (a tombstone stays for other devices). Idempotent: an unknown or already deleted id
/// succeeds without changing anything (null). A delete older than the stored edit loses and the stored exam
/// comes back, so the client restores it instead of dropping its copy (same rule as topic tests, ADR-0014).
/// </summary>
public sealed record DeleteMockExamCommand(Guid UserId, Guid Id, DateTimeOffset ModifiedAt) : ICommand<MockExamResponse?>;

internal sealed class DeleteMockExamHandler(IMockExamRepository exams, ITrackingUnitOfWork unitOfWork, IClock clock)
    : ICommandHandler<DeleteMockExamCommand, MockExamResponse?>
{
    public async Task<Result<MockExamResponse?>> Handle(DeleteMockExamCommand command, CancellationToken cancellationToken)
    {
        var exam = await exams.Get(command.Id, cancellationToken);
        if (exam is null)
        {
            return (MockExamResponse?)null;
        }

        if (exam.UserId != command.UserId)
        {
            return TrackingErrors.MockExamNotFound;
        }

        var now = clock.UtcNow;
        var sections = exam.Sections.Select(s => s.SectionCode).ToList();
        var topics = exam.Marks.Select(m => m.TopicId).Distinct().ToList();
        var wasDeleted = exam.IsDeleted;
        if (!exam.Delete(command.ModifiedAt, now))
        {
            return wasDeleted ? null : MockExamResponse.From(exam);
        }

        unitOfWork.Enqueue(new MockExamChanged(Guid.CreateVersion7(now), now, command.UserId, exam.Id, sections, topics));
        return await unitOfWork.TrySaveChanges(cancellationToken) ? (MockExamResponse?)null : TrackingErrors.MockExamConflict;
    }
}
