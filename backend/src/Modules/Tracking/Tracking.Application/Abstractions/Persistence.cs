using Nirengi.Modules.Tracking.Domain.Profiles;

namespace Nirengi.Modules.Tracking.Application.Abstractions;

public interface IStudyProfileRepository
{
    Task<StudyProfile?> Get(Guid userId, CancellationToken cancellationToken);

    void Add(StudyProfile profile);

    /// <summary>Deletes immediately (no SaveChanges); a no-op when there is nothing to delete.</summary>
    Task DeleteForUser(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Commits the Tracking changes in one transaction.</summary>
public interface ITrackingUnitOfWork
{
    /// <summary>
    /// False, with nothing written, when a concurrent request changed the same rows (row version) or took the
    /// same unique key first.
    /// </summary>
    Task<bool> TrySaveChanges(CancellationToken cancellationToken);
}
