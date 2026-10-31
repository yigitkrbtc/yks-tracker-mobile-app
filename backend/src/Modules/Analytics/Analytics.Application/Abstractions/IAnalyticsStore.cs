using Nirengi.Modules.Analytics.Application.Snapshots;

namespace Nirengi.Modules.Analytics.Application.Abstractions;

/// <summary>The analysis read model (schema <c>analytics</c>).</summary>
public interface IAnalyticsStore
{
    Task<UserAnalysis?> Get(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the student's whole analysis in one transaction, under a per-user lock so concurrent recomputes
    /// cannot interleave their rows.
    /// </summary>
    Task Replace(
        UserAnalysis analysis,
        IReadOnlyList<TopicSnapshot> topics,
        IReadOnlyList<SectionSnapshot> sections,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TopicSnapshot>> GetTopics(Guid userId, CancellationToken cancellationToken);

    Task<TopicSnapshot?> GetTopic(Guid userId, Guid topicId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SectionSnapshot>> GetSections(Guid userId, CancellationToken cancellationToken);

    /// <summary>Deletes immediately; a no-op when there is nothing to delete.</summary>
    Task DeleteForUser(Guid userId, CancellationToken cancellationToken);
}
