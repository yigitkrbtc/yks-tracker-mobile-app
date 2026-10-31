using Microsoft.EntityFrameworkCore;
using Nirengi.Modules.Analytics.Application.Abstractions;
using Nirengi.Modules.Analytics.Application.Snapshots;

namespace Nirengi.Modules.Analytics.Infrastructure.Persistence;

internal sealed class AnalyticsStore(AnalyticsDbContext db) : IAnalyticsStore
{
    public Task<UserAnalysis?> Get(Guid userId, CancellationToken cancellationToken) =>
        db.UserAnalyses.AsNoTracking().SingleOrDefaultAsync(a => a.UserId == userId, cancellationToken);

    public async Task Replace(
        UserAnalysis analysis,
        IReadOnlyList<TopicSnapshot> topics,
        IReadOnlyList<SectionSnapshot> sections,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended('analytics.user:' || {analysis.UserId.ToString()}, 0))",
            cancellationToken);

        await db.TopicSnapshots.Where(t => t.UserId == analysis.UserId).ExecuteDeleteAsync(cancellationToken);
        await db.SectionSnapshots.Where(s => s.UserId == analysis.UserId).ExecuteDeleteAsync(cancellationToken);
        await db.UserAnalyses.Where(a => a.UserId == analysis.UserId).ExecuteDeleteAsync(cancellationToken);

        db.UserAnalyses.Add(analysis);
        db.TopicSnapshots.AddRange(topics);
        db.SectionSnapshots.AddRange(sections);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<TopicSnapshot>> GetTopics(Guid userId, CancellationToken cancellationToken) =>
        await db.TopicSnapshots.AsNoTracking().Where(t => t.UserId == userId).ToListAsync(cancellationToken);

    public Task<TopicSnapshot?> GetTopic(Guid userId, Guid topicId, CancellationToken cancellationToken) =>
        db.TopicSnapshots.AsNoTracking().SingleOrDefaultAsync(t => t.UserId == userId && t.TopicId == topicId, cancellationToken);

    public async Task<IReadOnlyList<SectionSnapshot>> GetSections(Guid userId, CancellationToken cancellationToken) =>
        await db.SectionSnapshots.AsNoTracking().Where(s => s.UserId == userId).ToListAsync(cancellationToken);

    public async Task DeleteForUser(Guid userId, CancellationToken cancellationToken)
    {
        await db.TopicSnapshots.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.SectionSnapshots.Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.UserAnalyses.Where(a => a.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }
}
