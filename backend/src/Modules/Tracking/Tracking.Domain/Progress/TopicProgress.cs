namespace Nirengi.Modules.Tracking.Domain.Progress;

/// <summary>The student's own declaration about a topic. It never feeds the metrics (ADR-0018).</summary>
public enum TopicProgressStatus
{
    NotStarted,
    InProgress,
    Done,
}

/// <summary>
/// "Have I covered this topic?" per student and topic, keyed by the pair (no client id). Synced like the
/// records (ADR-0014): the newer client edit wins; going back to <see cref="TopicProgressStatus.NotStarted"/> is
/// an ordinary edit, so there are no tombstones.
/// </summary>
public sealed class TopicProgress : ISyncRecord
{
    private TopicProgress()
    {
    }

    public Guid UserId { get; private set; }

    public Guid TopicId { get; private set; }

    public TopicProgressStatus Status { get; private set; }

    public DateTimeOffset ClientModifiedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long ChangeSeq { get; private set; }

    public static TopicProgress Create(Guid userId, Guid topicId, TopicProgressStatus status, DateTimeOffset clientModifiedAt, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            TopicId = topicId,
            Status = status,
            ClientModifiedAt = EditTime.Of(clientModifiedAt, now),
            UpdatedAt = now,
        };

    /// <returns>False when an equal or newer edit is stored; the stored state stands.</returns>
    public bool Apply(TopicProgressStatus status, DateTimeOffset clientModifiedAt, DateTimeOffset now)
    {
        var editTime = EditTime.Of(clientModifiedAt, now);
        if (editTime <= ClientModifiedAt)
        {
            return false;
        }

        Status = status;
        ClientModifiedAt = editTime;
        UpdatedAt = now;
        return true;
    }

    public void AssignChangeSeq(long changeSeq) => ChangeSeq = changeSeq;
}
