using Nirengi.Modules.Recommendation.Domain.Plans;

namespace Nirengi.Modules.Recommendation.Application.Abstractions;

/// <summary>Plans and dismissals (schema <c>recommendation</c>).</summary>
public interface IRecommendationStore
{
    Task<StoredPlan?> GetPlan(Guid userId, DateOnly weekStart, CancellationToken cancellationToken);

    /// <summary>Inserts or replaces the week's plan, plus an optional dismissal, in one transaction under a per-user lock.</summary>
    Task SavePlan(StoredPlan plan, ItemFeedback? dismissal, CancellationToken cancellationToken);

    Task<IReadOnlyList<ItemFeedback>> GetFeedback(Guid userId, DateOnly weekStart, CancellationToken cancellationToken);

    /// <summary>Every dismissal of the user, for the data export.</summary>
    Task<IReadOnlyList<ItemFeedback>> GetAllFeedback(Guid userId, CancellationToken cancellationToken);

    /// <summary>Deletes immediately; a no-op when there is nothing to delete.</summary>
    Task DeleteForUser(Guid userId, CancellationToken cancellationToken);
}
