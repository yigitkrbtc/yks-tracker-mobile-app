using Nirengi.Modules.Tracking.Domain.TopicTests;

namespace Nirengi.Modules.Tracking.Application.Features.TopicTests;

public sealed record TopicResultResponse(
    Guid TopicId,
    int Total,
    int Correct,
    int Wrong,
    int Blank,
    decimal Net,
    int? MistakeKnowledge,
    int? MistakeAttention,
    int? MistakeTime);

/// <summary>The stored state of a test. A deleted test comes back as a tombstone: <c>Deleted</c> true, no results.</summary>
/// <param name="ChangeSeq">Position in the change feed; the sync cursor.</param>
public sealed record TopicTestResponse(
    Guid Id,
    DateOnly StudiedOn,
    Guid? SourceId,
    string? SourceName,
    bool IsRepeat,
    Difficulty? Difficulty,
    int? DurationMinutes,
    string? Note,
    IReadOnlyList<TopicResultResponse> Results,
    DateTimeOffset ClientModifiedAt,
    DateTimeOffset UpdatedAt,
    bool Deleted,
    long ChangeSeq)
{
    public static TopicTestResponse From(TopicTest t) => new(
        t.Id,
        t.StudiedOn,
        t.SourceId,
        t.SourceName,
        t.IsRepeat,
        t.Difficulty,
        t.DurationMinutes,
        t.Note,
        [.. t.Results.OrderBy(r => r.TopicId).Select(r => new TopicResultResponse(
            r.TopicId, r.Total, r.Correct, r.Wrong, r.Blank, r.Net, r.MistakeKnowledge, r.MistakeAttention, r.MistakeTime))],
        t.ClientModifiedAt,
        t.UpdatedAt,
        t.IsDeleted,
        t.ChangeSeq);
}
