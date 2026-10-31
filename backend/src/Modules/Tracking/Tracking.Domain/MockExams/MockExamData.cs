using Nirengi.Modules.Tracking.Domain.TopicTests;

namespace Nirengi.Modules.Tracking.Domain.MockExams;

public enum MockExamKind
{
    Tyt = 1,
    Ayt = 2,

    /// <summary>A single-section exam (e.g. only TYT Matematik); its question count may differ from ÖSYM's.</summary>
    Branch = 3,
}

/// <summary>How much of a section's lost questions the student tagged with topics; analysis (M11c) uses only Full.</summary>
public enum TaggingStatus
{
    None = 0,
    Partial = 1,
    Full = 2,
}

/// <summary>One solved section: correct and wrong as entered, blank follows from the question count.</summary>
public sealed record MockSectionData(string SectionCode, int QuestionCount, int Correct, int Wrong);

/// <summary>Wrong and blank answers of one section put on one topic; mistake reasons are optional counts of the wrong ones.</summary>
public sealed record TopicMarkData(
    string SectionCode,
    Guid TopicId,
    int Wrong,
    int Blank,
    int? MistakeKnowledge = null,
    int? MistakeAttention = null,
    int? MistakeTime = null);

/// <summary>
/// The full content of a mock exam as the client sends it (whole-document PUT, ADR-0005). Sections the student
/// did not solve are simply absent (never zero). Topic marks are optional and may come in a later edit.
/// </summary>
public sealed record MockExamData(
    MockExamKind Kind,
    DateOnly TakenOn,
    Guid? SourceId,
    string? SourceName,
    string? Title,
    int? DurationMinutes,
    string? Note,
    IReadOnlyList<MockSectionData> Sections,
    IReadOnlyList<TopicMarkData> Marks);

/// <summary>Limits shared by the validator, the template check and the aggregate's guards.</summary>
public static class MockExamRules
{
    public const int MaxSections = 30;

    public const int MaxMarks = 300;

    /// <summary>Upper bound for a branch exam's own question count.</summary>
    public const int MaxBranchQuestions = 80;

    public const int TitleMaxLength = 100;

    public const int MaxDurationMinutes = TopicTestRules.MaxDurationMinutes;

    public const int NoteMaxLength = TopicTestRules.NoteMaxLength;

    public const int SourceNameMaxLength = TopicTestRules.SourceNameMaxLength;

    public static decimal Net(int correct, int wrong) => TopicTestRules.Net(correct, wrong);

    public static TaggingStatus Tagging(int lost, int tagged) =>
        tagged == lost ? TaggingStatus.Full : tagged == 0 ? TaggingStatus.None : TaggingStatus.Partial;
}
