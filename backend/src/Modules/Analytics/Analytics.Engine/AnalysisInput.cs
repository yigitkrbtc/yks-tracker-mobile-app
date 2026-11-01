namespace Nirengi.Modules.Analytics.Engine;

/// <summary>Everything the engine needs for one student. No clock, no I/O: <paramref name="Today"/> is the reference day (Europe/Istanbul).</summary>
/// <param name="ActiveTests">Test codes of the student's score types; the priority list is limited to their subjects.</param>
public sealed record AnalysisInput(
    DateOnly Today,
    AnalysisCatalog Catalog,
    IReadOnlyCollection<string> ActiveTests,
    IReadOnlyList<TopicRecord> TopicRecords,
    IReadOnlyList<MockExamRecord> MockExams);

/// <summary>One topic line of a topic test (S3). <paramref name="TestId"/> identifies the test it belongs to.</summary>
public sealed record TopicRecord(
    Guid TestId,
    Guid TopicId,
    DateOnly Date,
    int Total,
    int Correct,
    int Wrong,
    int Blank,
    bool IsRepeat);

/// <summary>A mock exam with only the sections the student solved (S4).</summary>
public sealed record MockExamRecord(Guid Id, DateOnly Date, IReadOnlyList<MockSectionRecord> Sections);

/// <param name="IsFullyTagged">Every wrong and blank answer of the section is tagged to a topic.</param>
public sealed record MockSectionRecord(
    string Code,
    int QuestionCount,
    int Correct,
    int Wrong,
    bool IsFullyTagged,
    IReadOnlyList<TopicMark> Marks);

public sealed record TopicMark(Guid TopicId, int Wrong, int Blank);

/// <summary>The curriculum slice the engine reads. Topic order is the display order and breaks ranking ties.</summary>
public sealed record AnalysisCatalog(
    IReadOnlyList<CatalogTopicInfo> Topics,
    IReadOnlyList<CatalogSubjectInfo> Subjects,
    IReadOnlyList<CatalogSectionInfo> Sections);

/// <param name="ExpectedQuestions">q_i: expected questions in the exam (ÖSYM 2022–2026 mean).</param>
public sealed record CatalogTopicInfo(Guid Id, string SubjectCode, double ExpectedQuestions);

public sealed record CatalogSubjectInfo(string Code, IReadOnlyList<string> Tests);

/// <param name="ParentCode">Null for the root section of a test.</param>
/// <param name="SubjectCodes">Subjects whose topics the section asks.</param>
public sealed record CatalogSectionInfo(
    string Code,
    string TestCode,
    string? ParentCode,
    IReadOnlyList<string> SubjectCodes,
    int QuestionCount);
