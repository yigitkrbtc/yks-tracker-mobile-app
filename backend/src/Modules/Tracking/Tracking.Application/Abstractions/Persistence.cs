using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.Modules.Tracking.Domain.MockExams;
using Nirengi.Modules.Tracking.Domain.Profiles;
using Nirengi.Modules.Tracking.Domain.Progress;
using Nirengi.Modules.Tracking.Domain.TopicTests;

namespace Nirengi.Modules.Tracking.Application.Abstractions;

public interface IStudyProfileRepository
{
    Task<StudyProfile?> Get(Guid userId, CancellationToken cancellationToken);

    void Add(StudyProfile profile);

    /// <summary>Deletes immediately (no SaveChanges); a no-op when there is nothing to delete.</summary>
    Task DeleteForUser(Guid userId, CancellationToken cancellationToken);
}

public interface ITopicTestRepository
{
    /// <summary>Any user's test, deleted ones included; the caller checks ownership.</summary>
    Task<TopicTest?> Get(Guid id, CancellationToken cancellationToken);

    void Add(TopicTest test);

    /// <summary>The user's tests changed after <paramref name="since"/>, tombstones included, in change-feed order.</summary>
    Task<IReadOnlyList<TopicTest>> GetChanges(Guid userId, long since, int limit, CancellationToken cancellationToken);

    /// <summary>Deletes immediately (no SaveChanges), results included; a no-op when there is nothing to delete.</summary>
    Task DeleteForUser(Guid userId, CancellationToken cancellationToken);
}

public interface IMockExamRepository
{
    /// <summary>Any user's exam, deleted ones included; the caller checks ownership.</summary>
    Task<MockExam?> Get(Guid id, CancellationToken cancellationToken);

    void Add(MockExam exam);

    /// <summary>The user's exams changed after <paramref name="since"/>, tombstones included, in change-feed order.</summary>
    Task<IReadOnlyList<MockExam>> GetChanges(Guid userId, long since, int limit, CancellationToken cancellationToken);

    /// <summary>Deletes immediately (no SaveChanges), sections and marks included; a no-op when there is nothing to delete.</summary>
    Task DeleteForUser(Guid userId, CancellationToken cancellationToken);
}

public interface ITopicProgressRepository
{
    Task<TopicProgress?> Get(Guid userId, Guid topicId, CancellationToken cancellationToken);

    void Add(TopicProgress progress);

    /// <summary>The user's marks changed after <paramref name="since"/>, in change-feed order.</summary>
    Task<IReadOnlyList<TopicProgress>> GetChanges(Guid userId, long since, int limit, CancellationToken cancellationToken);

    /// <summary>Deletes immediately (no SaveChanges); a no-op when there is nothing to delete.</summary>
    Task DeleteForUser(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Commits the Tracking changes in one transaction. Changed synced records (topic tests, mock exams, topic marks) get their change-feed position here,
/// under a per-user lock, so a user's positions commit in order and GET /v1/sync never skips a write.
/// </summary>
public interface ITrackingUnitOfWork
{
    /// <summary>Queued into this module's outbox and written by the next save (ADR-0002).</summary>
    void Enqueue(IIntegrationEvent integrationEvent);

    /// <summary>
    /// False, with nothing written, when a concurrent request changed the same rows (row version) or took the
    /// same unique key first.
    /// </summary>
    Task<bool> TrySaveChanges(CancellationToken cancellationToken);
}
