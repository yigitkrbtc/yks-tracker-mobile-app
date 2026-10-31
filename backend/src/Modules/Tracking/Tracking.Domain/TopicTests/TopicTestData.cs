namespace Nirengi.Modules.Tracking.Domain.TopicTests;

public enum Difficulty
{
    Easy = 1,
    Medium = 2,
    Hard = 3,
}

/// <summary>One topic's answers in a test. Mistake reasons are optional counts of the wrong answers.</summary>
public sealed record TopicResultData(
    Guid TopicId,
    int Total,
    int Correct,
    int Wrong,
    int Blank,
    int? MistakeKnowledge = null,
    int? MistakeAttention = null,
    int? MistakeTime = null);

/// <summary>
/// The full content of a topic test as the client sends it (whole-document PUT, ADR-0005). The source is either
/// a listed one (<see cref="SourceId"/>) or free text (<see cref="SourceName"/>), never both and never neither.
/// </summary>
public sealed record TopicTestData(
    DateOnly StudiedOn,
    Guid? SourceId,
    string? SourceName,
    bool IsRepeat,
    Difficulty? Difficulty,
    int? DurationMinutes,
    string? Note,
    IReadOnlyList<TopicResultData> Results);

/// <summary>Limits shared by the validator and the aggregate's guards.</summary>
public static class TopicTestRules
{
    public const int MaxTopics = 20;

    public const int MaxQuestionsPerTopic = 200;

    public const int MaxDurationMinutes = 600;

    public const int NoteMaxLength = 500;

    public const int SourceNameMaxLength = 100;

    /// <summary>How far back a test may be dated.</summary>
    public const int MaxDaysBack = 365;

    /// <summary>Four wrong answers cancel one right one (ÖSYM rule, also for topic tests: k = 0.25).</summary>
    public const decimal WrongPenalty = 0.25m;

    // Türkiye is UTC+3 all year (no DST since 2016).
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    public static DateOnly TodayInTurkey(DateTimeOffset utcNow) => DateOnly.FromDateTime(utcNow.ToOffset(TurkeyOffset).DateTime);

    public static bool IsAllowedStudyDate(DateOnly date, DateTimeOffset utcNow)
    {
        var today = TodayInTurkey(utcNow);
        return date <= today && date >= today.AddDays(-MaxDaysBack);
    }

    public static decimal Net(int correct, int wrong) => correct - (wrong * WrongPenalty);
}
