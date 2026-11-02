using Nirengi.Modules.Curriculum.Domain.Calendar;
using Nirengi.Modules.Curriculum.Domain.Sources;

namespace Nirengi.Modules.Curriculum.Domain.Versions;

/// <summary>A topic as the admin edits it. The code is its permanent key; the subject is the code's prefix.</summary>
public sealed record TopicState(
    string Code,
    string SubjectCode,
    string Name,
    IReadOnlyList<string> Subtopics,
    int GradeMin,
    int GradeMax,
    string? Description,
    int SortOrder,
    bool IsActive);

public sealed record SourceState(string Code, string Name, SourceKind Kind, int SortOrder, bool IsActive);

/// <summary>
/// The admin-editable part of the curriculum (ADR-0019): topics, sources and exam calendars. Exam structure and
/// weights stay seed-only. Also the shape of a published version's snapshot.
/// </summary>
public sealed record CurriculumContent(
    IReadOnlyList<TopicState> Topics,
    IReadOnlyList<SourceState> Sources,
    IReadOnlyList<ExamCalendar> Calendars)
{
    public static readonly CurriculumContent Empty = new([], [], []);
}

public enum LineageRelation
{
    /// <summary>The old topic was folded into the new one; its records count for the new topic.</summary>
    Merged = 0,

    /// <summary>The old topic was divided; its records stay with the retired topic.</summary>
    Split = 1,
}

public sealed record LineageLink(string FromCode, string ToCode, LineageRelation Relation);
