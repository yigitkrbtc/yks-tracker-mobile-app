using Microsoft.EntityFrameworkCore;
using Npgsql;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.BuildingBlocks.Application.Events;
using Nirengi.Modules.Tracking.Domain.Profiles;
using Nirengi.Modules.Tracking.Domain.TopicTests;

namespace Nirengi.Modules.Tracking.Infrastructure.Persistence;

internal sealed class StudyProfileRepository(TrackingDbContext db) : IStudyProfileRepository
{
    public Task<StudyProfile?> Get(Guid userId, CancellationToken cancellationToken) =>
        db.StudyProfiles.SingleOrDefaultAsync(p => p.Id == userId, cancellationToken);

    public void Add(StudyProfile profile) => db.StudyProfiles.Add(profile);

    public Task DeleteForUser(Guid userId, CancellationToken cancellationToken) =>
        db.StudyProfiles.Where(p => p.Id == userId).ExecuteDeleteAsync(cancellationToken);
}

internal sealed class TopicTestRepository(TrackingDbContext db) : ITopicTestRepository
{
    public Task<TopicTest?> Get(Guid id, CancellationToken cancellationToken) =>
        db.TopicTests.Include(t => t.Results).SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public void Add(TopicTest test) => db.TopicTests.Add(test);

    public async Task<IReadOnlyList<TopicTest>> GetChanges(Guid userId, long since, int limit, CancellationToken cancellationToken) =>
        await db.TopicTests.AsNoTracking()
            .Include(t => t.Results)
            .Where(t => t.UserId == userId && t.ChangeSeq > since)
            .OrderBy(t => t.ChangeSeq)
            .Take(limit)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

    // Results go with their test (ON DELETE CASCADE).
    public Task DeleteForUser(Guid userId, CancellationToken cancellationToken) =>
        db.TopicTests.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
}

internal sealed class TrackingUnitOfWork(TrackingDbContext db) : ITrackingUnitOfWork
{
    public void Enqueue(IIntegrationEvent integrationEvent) => db.Enqueue(integrationEvent);

    public async Task<bool> TrySaveChanges(CancellationToken cancellationToken)
    {
        try
        {
            var changed = db.ChangeTracker.Entries<TopicTest>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified)
                .Select(e => e.Entity)
                .ToList();
            if (changed.Count == 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            foreach (var userId in changed.Select(t => t.UserId).Distinct().Order())
            {
                await db.Database.ExecuteSqlAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended('tracking.user:' || {userId.ToString()}, 0))",
                    cancellationToken);
            }

            foreach (var test in changed)
            {
                test.AssignChangeSeq(await db.Database
                    .SqlQueryRaw<long>($"SELECT nextval('{TrackingDbContext.SchemaName}.{TrackingDbContext.ChangeSequenceName}') AS \"Value\"")
                    .SingleAsync(cancellationToken));
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }
}
