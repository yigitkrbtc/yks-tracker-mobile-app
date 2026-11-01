using Nirengi.Modules.Recommendation.Domain.Plans;

namespace Nirengi.Modules.Recommendation.Application.Abstractions;

/// <summary>Plans and dismissals (schema <c>recommendation</c>).</summary>
public interface IRecommendationStore
{
    Task<StoredPlan?> GetPlan(Guid userId, DateOnly weekStart, CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction holding a per-user lock, so a plan's read-modify-write
    /// (including a dismissal) never interleaves with another request of the same user.
    /// </summary>
    Task<T> InUserLock<T>(Guid userId, Func<Task<T>> work, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces the week's plan, plus an optional dismissal. Call only inside <see cref="InUserLock"/>.</summary>
    Task SavePlan(StoredPlan plan, ItemFeedback? dismissal, CancellationToken cancellationToken);

    Task<IReadOnlyList<ItemFeedback>> GetFeedback(Guid userId, DateOnly weekStart, CancellationToken cancellationToken);

    /// <summary>Every dismissal of the user, for the data export.</summary>
    Task<IReadOnlyList<ItemFeedback>> GetAllFeedback(Guid userId, CancellationToken cancellationToken);

    /// <summary>Deletes immediately; a no-op when there is nothing to delete.</summary>
    Task DeleteForUser(Guid userId, CancellationToken cancellationToken);
}
