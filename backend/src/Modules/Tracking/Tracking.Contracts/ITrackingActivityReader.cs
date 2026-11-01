namespace Nirengi.Modules.Tracking.Contracts;

/// <summary>Questions a student recorded on a topic on one day (topic tests only; deleted ones excluded).</summary>
public sealed record TopicActivity(DateOnly Day, Guid TopicId, int Questions);

/// <summary>A solved mock exam section whose wrong and blank answers are not all tagged to topics yet.</summary>
public sealed record PendingTagging(Guid MockExamId, DateOnly TakenOn, string SectionCode, int WrongAndBlank);

/// <summary>The student's own mark on a topic: "NotStarted", "InProgress" or "Done". Never a metric input.</summary>
public sealed record DeclaredProgress(Guid TopicId, string Status);

/// <summary>Read access to recent activity for recommendations (S6), without Tracking's tables or types.</summary>
public interface ITrackingActivityReader
{
    /// <param name="from">First day, inclusive.</param>
    /// <param name="to">Last day, inclusive.</param>
    Task<IReadOnlyList<TopicActivity>> GetTopicActivity(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Sections of live mock exams taken on or after <paramref name="from"/>, newest exam first.</summary>
    Task<IReadOnlyList<PendingTagging>> GetPendingTagging(Guid userId, DateOnly from, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeclaredProgress>> GetProgress(Guid userId, CancellationToken cancellationToken);
}
