using Microsoft.EntityFrameworkCore;
using Npgsql;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Domain.Profiles;

namespace Nirengi.Modules.Tracking.Infrastructure.Persistence;

internal sealed class StudyProfileRepository(TrackingDbContext db) : IStudyProfileRepository
{
    public Task<StudyProfile?> Get(Guid userId, CancellationToken cancellationToken) =>
        db.StudyProfiles.SingleOrDefaultAsync(p => p.Id == userId, cancellationToken);

    public void Add(StudyProfile profile) => db.StudyProfiles.Add(profile);

    public Task DeleteForUser(Guid userId, CancellationToken cancellationToken) =>
        db.StudyProfiles.Where(p => p.Id == userId).ExecuteDeleteAsync(cancellationToken);
}

internal sealed class TrackingUnitOfWork(TrackingDbContext db) : ITrackingUnitOfWork
{
    public async Task<bool> TrySaveChanges(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
