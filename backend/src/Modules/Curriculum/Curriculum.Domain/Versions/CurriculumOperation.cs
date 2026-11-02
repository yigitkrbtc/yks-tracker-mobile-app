using System.Text.Json.Serialization;
using Nirengi.Modules.Curriculum.Domain.Calendar;
using Nirengi.Modules.Curriculum.Domain.Sources;

namespace Nirengi.Modules.Curriculum.Domain.Versions;

/// <summary>One change in a draft (ADR-0019). A draft is an ordered list; publishing applies it in one transaction.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AddTopic), "topic.add")]
[JsonDerivedType(typeof(UpdateTopic), "topic.update")]
[JsonDerivedType(typeof(RetireTopic), "topic.retire")]
[JsonDerivedType(typeof(ReactivateTopic), "topic.reactivate")]
[JsonDerivedType(typeof(MergeTopics), "topic.merge")]
[JsonDerivedType(typeof(SplitTopic), "topic.split")]
[JsonDerivedType(typeof(AddSource), "source.add")]
[JsonDerivedType(typeof(UpdateSource), "source.update")]
[JsonDerivedType(typeof(RetireSource), "source.retire")]
[JsonDerivedType(typeof(ReactivateSource), "source.reactivate")]
[JsonDerivedType(typeof(SetCalendar), "calendar.set")]
public abstract record CurriculumOperation;

/// <param name="Code">Left empty by the admin: the next free number of the subject is assigned when the draft is saved.</param>
/// <param name="SortOrder">Null puts the topic last in its subject.</param>
public sealed record AddTopic(
    string? Code,
    string SubjectCode,
    string Name,
    IReadOnlyList<string> Subtopics,
    int GradeMin,
    int GradeMax,
    string? Description,
    int? SortOrder = null) : CurriculumOperation;

/// <param name="SortOrder">Null keeps the current position.</param>
public sealed record UpdateTopic(
    string Code,
    string Name,
    IReadOnlyList<string> Subtopics,
    int GradeMin,
    int GradeMax,
    string? Description,
    int? SortOrder = null) : CurriculumOperation;

public sealed record RetireTopic(string Code) : CurriculumOperation;

public sealed record ReactivateTopic(string Code) : CurriculumOperation;

/// <summary>Retires <paramref name="FromCodes"/>; their records count for <paramref name="ToCode"/> in the analysis.</summary>
public sealed record MergeTopics(IReadOnlyList<string> FromCodes, string ToCode) : CurriculumOperation;

/// <summary>Retires <paramref name="FromCode"/>; the new topics (usually added earlier in the draft) start empty.</summary>
public sealed record SplitTopic(string FromCode, IReadOnlyList<string> ToCodes) : CurriculumOperation;

public sealed record AddSource(string Code, string Name, SourceKind Kind) : CurriculumOperation;

public sealed record UpdateSource(string Code, string Name, SourceKind Kind) : CurriculumOperation;

public sealed record RetireSource(string Code) : CurriculumOperation;

public sealed record ReactivateSource(string Code) : CurriculumOperation;

/// <summary>Replaces one exam year's calendar (e.g. when ÖSYM announces the dates).</summary>
public sealed record SetCalendar(ExamCalendar Calendar) : CurriculumOperation;
