using Nirengi.Modules.Tracking.Domain.MockExams;

namespace Nirengi.Modules.Tracking.Application.Features.MockExams;

public sealed record MockSectionResponse(
    string SectionCode,
    int QuestionCount,
    int Correct,
    int Wrong,
    int Blank,
    decimal Net,
    TaggingStatus TaggingStatus);

public sealed record TopicMarkResponse(
    string SectionCode,
    Guid TopicId,
    int Wrong,
    int Blank,
    int? MistakeKnowledge,
    int? MistakeAttention,
    int? MistakeTime);

/// <summary>The stored state of a mock exam. A deleted one comes back as a tombstone: <c>Deleted</c> true, no sections.</summary>
/// <param name="ChangeSeq">Position in the change feed; the sync cursor.</param>
public sealed record MockExamResponse(
    Guid Id,
    MockExamKind Kind,
    DateOnly TakenOn,
    Guid? SourceId,
    string? SourceName,
    string? Title,
    int? DurationMinutes,
    string? Note,
    IReadOnlyList<MockSectionResponse> Sections,
    IReadOnlyList<TopicMarkResponse> Marks,
    DateTimeOffset ClientModifiedAt,
    DateTimeOffset UpdatedAt,
    bool Deleted,
    long ChangeSeq)
{
    public static MockExamResponse From(MockExam e) => new(
        e.Id,
        e.Kind,
        e.TakenOn,
        e.SourceId,
        e.SourceName,
        e.Title,
        e.DurationMinutes,
        e.Note,
        [.. e.Sections.OrderBy(s => s.SectionCode, StringComparer.Ordinal).Select(s => new MockSectionResponse(
            s.SectionCode, s.QuestionCount, s.Correct, s.Wrong, s.Blank, s.Net, s.TaggingStatus))],
        [.. e.Marks.OrderBy(m => m.SectionCode, StringComparer.Ordinal).ThenBy(m => m.TopicId).Select(m => new TopicMarkResponse(
            m.SectionCode, m.TopicId, m.Wrong, m.Blank, m.MistakeKnowledge, m.MistakeAttention, m.MistakeTime))],
        e.ClientModifiedAt,
        e.UpdatedAt,
        e.IsDeleted,
        e.ChangeSeq);
}
