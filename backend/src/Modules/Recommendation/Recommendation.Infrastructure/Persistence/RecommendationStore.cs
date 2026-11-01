using Microsoft.EntityFrameworkCore;
using Nirengi.Modules.Recommendation.Application.Abstractions;
using Nirengi.Modules.Recommendation.Domain.Plans;

namespace Nirengi.Modules.Recommendation.Infrastructure.Persistence;

internal sealed class RecommendationStore(RecommendationDbContext db) : IRecommendationStore
{
    public Task<StoredPlan?> GetPlan(Guid userId, DateOnly weekStart, CancellationToken cancellationToken) =>
        db.Plans.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId && p.WeekStart == weekStart, cancellationToken);

    public async Task<T> InUserLock<T>(Guid userId, Func<Task<T>> work, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended('recommendation.user:' || {userId.ToString()}, 0))",
            cancellationToken);
        try
        {
            var result = await work();
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    public async Task SavePlan(StoredPlan plan, ItemFeedback? dismissal, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("SavePlan must run inside InUserLock.");
        }

        await db.Plans.Where(p => p.UserId == plan.UserId && p.WeekStart == plan.WeekStart).ExecuteDeleteAsync(cancellationToken);
        db.Plans.Add(plan);
        if (dismissal is not null
            && !await db.Feedback.AnyAsync(
                f => f.UserId == dismissal.UserId && f.WeekStart == dismissal.WeekStart && f.ItemKey == dismissal.ItemKey,
                cancellationToken))
        {
            db.Feedback.Add(dismissal);
        }

        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<ItemFeedback>> GetFeedback(Guid userId, DateOnly weekStart, CancellationToken cancellationToken) =>
        await db.Feedback.AsNoTracking().Where(f => f.UserId == userId && f.WeekStart == weekStart).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ItemFeedback>> GetAllFeedback(Guid userId, CancellationToken cancellationToken) =>
        await db.Feedback.AsNoTracking().Where(f => f.UserId == userId).OrderBy(f => f.At).ToListAsync(cancellationToken);

    public async Task DeleteForUser(Guid userId, CancellationToken cancellationToken)
    {
        await db.Feedback.Where(f => f.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.Plans.Where(p => p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }
}
