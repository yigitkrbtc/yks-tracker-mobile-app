namespace Nirengi.Modules.Tracking.Contracts;

/// <summary>
/// What a student's analysis depends on: the last change-feed position of their records and when their profile
/// last changed. Equal stamps mean the same input, so Analytics can skip a recompute.
/// </summary>
public sealed record TrackingStamp(long ChangeSeq, DateTimeOffset? ProfileUpdatedAt);

/// <summary>A student's live (not deleted) records, read for analysis (S5).</summary>
/// <param name="ScoreTypes">Score type codes of the profile (match Curriculum's codes); empty before onboarding.</param>
public sealed record StudentHistory(
    IReadOnlyList<string> ScoreTypes,
    IReadOnlyList<HistoryTopicResult> TopicResults,
    IReadOnlyList<HistoryMockExam> MockExams);

public sealed record HistoryTopicResult(
    Guid TopicTestId,
    Guid TopicId,
    DateOnly StudiedOn,
    int Total,
    int Correct,
    int Wrong,
    int Blank,
    bool IsRepeat);

public sealed record HistoryMockExam(Guid Id, DateOnly TakenOn, IReadOnlyList<HistoryMockSection> Sections);

/// <param name="IsFullyTagged">Every wrong and blank answer of the section is tagged to a topic.</param>
public sealed record HistoryMockSection(
    string SectionCode,
    int QuestionCount,
    int Correct,
    int Wrong,
    bool IsFullyTagged,
    IReadOnlyList<HistoryTopicMark> Marks);

public sealed record HistoryTopicMark(Guid TopicId, int Wrong, int Blank);

/// <summary>Read access to a student's records for the analysis engine, without Tracking's tables or types.</summary>
public interface ITrackingAnalyticsReader
{
    Task<TrackingStamp> GetStamp(Guid userId, CancellationToken cancellationToken);

    Task<StudentHistory> GetHistory(Guid userId, CancellationToken cancellationToken);
}
