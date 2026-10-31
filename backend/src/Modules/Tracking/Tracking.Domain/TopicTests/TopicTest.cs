using Nirengi.BuildingBlocks.Domain.Entities;

namespace Nirengi.Modules.Tracking.Domain.TopicTests;

/// <summary>
/// One sitting of a topic test (a fascicle, a page of questions), recorded offline-first: the id is a client
/// UUID v7 and every change carries the client's edit time, so a late upload from an old device never
/// overwrites a newer edit (last writer by edit time wins, ADR-0005). Deletion keeps a tombstone for sync.
/// </summary>
public sealed class TopicTest : AggregateRoot<Guid>, ISyncRecord
{
    private readonly List<TopicResult> _results = [];

    private TopicTest()
    {
    }

    public Guid UserId { get; private set; }

    public DateOnly StudiedOn { get; private set; }

    public Guid? SourceId { get; private set; }

    public string? SourceName { get; private set; }

    /// <summary>The same book solved again; down-weighted in mastery because it inflates accuracy (M0).</summary>
    public bool IsRepeat { get; private set; }

    public Difficulty? Difficulty { get; private set; }

    public int? DurationMinutes { get; private set; }

    public string? Note { get; private set; }

    /// <summary>When the client last edited or deleted it, capped at server time; decides which write wins.</summary>
    public DateTimeOffset ClientModifiedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Position in the change feed (GET /v1/sync); assigned by persistence on every write.</summary>
    public long ChangeSeq { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public IReadOnlyList<TopicResult> Results => _results;

    public static TopicTest Create(Guid id, Guid userId, TopicTestData data, DateTimeOffset clientModifiedAt, DateTimeOffset now)
    {
        var test = new TopicTest { Id = id, UserId = userId, CreatedAt = now };
        test.Set(data, EditTime.Of(clientModifiedAt, now), now);
        return test;
    }

    /// <summary>Replaces the content unless an equal or newer edit is already stored.</summary>
    /// <returns>False when nothing changed (a replay or a stale write); the stored state stands.</returns>
    public bool Apply(TopicTestData data, DateTimeOffset clientModifiedAt, DateTimeOffset now)
    {
        var editTime = EditTime.Of(clientModifiedAt, now);
        if (editTime <= ClientModifiedAt)
        {
            return false;
        }

        DeletedAt = null;
        Set(data, editTime, now);
        return true;
    }

    /// <returns>False when already deleted or a newer edit is stored.</returns>
    public bool Delete(DateTimeOffset clientModifiedAt, DateTimeOffset now)
    {
        var editTime = EditTime.Of(clientModifiedAt, now);
        if (IsDeleted || editTime <= ClientModifiedAt)
        {
            return false;
        }

        _results.Clear();
        ClientModifiedAt = editTime;
        DeletedAt = now;
        UpdatedAt = now;
        return true;
    }

    public void AssignChangeSeq(long changeSeq) => ChangeSeq = changeSeq;

    private void Set(TopicTestData data, DateTimeOffset editTime, DateTimeOffset now)
    {
        Guard(data);
        StudiedOn = data.StudiedOn;
        SourceId = data.SourceId;
        SourceName = data.SourceId.HasValue ? null : data.SourceName!.Trim();
        IsRepeat = data.IsRepeat;
        Difficulty = data.Difficulty;
        DurationMinutes = data.DurationMinutes;
        Note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
        ClientModifiedAt = editTime;
        UpdatedAt = now;

        // Update rows in place (key = test + topic) instead of delete-and-insert.
        var incoming = data.Results.ToDictionary(r => r.TopicId);
        _results.RemoveAll(r => !incoming.ContainsKey(r.TopicId));
        foreach (var result in _results)
        {
            result.Update(this, incoming[result.TopicId]);
            incoming.Remove(result.TopicId);
        }

        _results.AddRange(incoming.Values.Select(r => TopicResult.Create(this, r)));
    }

    /// <summary>The validator rejects bad input first; these guards only catch programming errors.</summary>
    private static void Guard(TopicTestData data)
    {
        if (data.Results.Count is 0 or > TopicTestRules.MaxTopics)
        {
            throw new ArgumentException($"A test has 1 to {TopicTestRules.MaxTopics} topics.", nameof(data));
        }

        if (data.Results.Select(r => r.TopicId).Distinct().Count() != data.Results.Count)
        {
            throw new ArgumentException("A topic appears twice in one test.", nameof(data));
        }

        if (data.SourceId.HasValue == !string.IsNullOrWhiteSpace(data.SourceName))
        {
            throw new ArgumentException("Exactly one of source id and source name is required.", nameof(data));
        }

        foreach (var r in data.Results)
        {
            var reasons = (r.MistakeKnowledge ?? 0) + (r.MistakeAttention ?? 0) + (r.MistakeTime ?? 0);
            if (r.Total is < 1 or > TopicTestRules.MaxQuestionsPerTopic
                || r.Correct < 0 || r.Wrong < 0 || r.Blank < 0
                || r.Correct + r.Wrong + r.Blank != r.Total
                || r.MistakeKnowledge < 0 || r.MistakeAttention < 0 || r.MistakeTime < 0
                || reasons > r.Wrong)
            {
                throw new ArgumentException($"Invalid answer counts for topic {r.TopicId}.", nameof(data));
            }
        }
    }
}
