using Nirengi.Modules.Analytics.Engine;

namespace Nirengi.Modules.Analytics.Application.Snapshots;

/// <summary>
/// One student's last analysis (read model, ADR-0016). <see cref="SourceStamp"/>, <see cref="ComputedOn"/> and
/// <see cref="RuleSetVersion"/> together decide whether it is still current.
/// </summary>
public sealed class UserAnalysis
{
    public Guid UserId { get; set; }

    public string RuleSetVersion { get; set; } = null!;

    /// <summary>Tracking change position + profile time + curriculum version the analysis was computed from.</summary>
    public string SourceStamp { get; set; } = null!;

    /// <summary>The reference day (Europe/Istanbul); decay and freshness depend on it.</summary>
    public DateOnly ComputedOn { get; set; }

    public DateTimeOffset ComputedAt { get; set; }

    public int InvalidRecords { get; set; }

    /// <summary>JSON of <see cref="AnalysisSummary"/>.</summary>
    public string Summary { get; set; } = null!;
}

/// <summary>A topic the student has data or a mock signal for. Typed columns for queries, details as JSON.</summary>
public sealed class TopicSnapshot
{
    public Guid UserId { get; set; }

    public Guid TopicId { get; set; }

    public double? Mastery { get; set; }

    public double? MasteryLow { get; set; }

    public double? MasteryHigh { get; set; }

    public double? Confidence { get; set; }

    public ConfidenceLevel ConfidenceLevel { get; set; }

    public MasteryBand Band { get; set; }

    public TrendLabel Trend { get; set; }

    public double? Priority { get; set; }

    /// <summary>JSON of <see cref="TopicDetails"/>.</summary>
    public string Details { get; set; } = null!;
}

/// <summary>M11a/b of one mock section key.</summary>
public sealed class SectionSnapshot
{
    public Guid UserId { get; set; }

    public string SectionCode { get; set; } = null!;

    public int Exams { get; set; }

    public bool IsSufficient { get; set; }

    public double Accuracy { get; set; }

    public TrendLabel Trend { get; set; }

    public double? PracticeGap { get; set; }

    public bool GapWarning { get; set; }

    /// <summary>JSON of <see cref="SectionDto"/>.</summary>
    public string Details { get; set; } = null!;
}

public sealed record AnalysisSummary(
    IReadOnlyList<AggregateDto> Tests,
    IReadOnlyList<AggregateDto> Subjects,
    IReadOnlyList<PriorityDto> Priorities,
    IReadOnlyList<Guid> LowDataTopics);

/// <summary>M7 lite for a subject or a test.</summary>
/// <param name="IsActive">Part of the student's score types.</param>
public sealed record AggregateDto(
    string Code,
    bool IsActive,
    double Mastery,
    double MasteryLow,
    double MasteryHigh,
    double Coverage,
    int StudiedTopics,
    int TopicCount);

/// <param name="Gain">Expected net gain part of the priority (q × gap × learnability).</param>
/// <param name="MockBoost">Part that comes from mock exam misses (M11c).</param>
public sealed record PriorityDto(Guid TopicId, double Priority, double Mastery, double ExpectedQuestions, double Gain, double MockBoost);

/// <summary>Everything the topic card shows besides the curriculum fields the client already has.</summary>
/// <param name="QuestionsToNextLevel">Fresh questions that would lift the confidence to the next level; null at the top.</param>
public sealed record TopicDetails(
    double? Mastery,
    double? MasteryLow,
    double? MasteryHigh,
    double? MarkingRate,
    bool Optimistic,
    double? ExpectedNetPerQuestion,
    double? EffectiveQuestions,
    double? Confidence,
    ConfidenceLevel ConfidenceLevel,
    int? QuestionsToNextLevel,
    MasteryBand Band,
    TrendDto Trend,
    double? Priority,
    TotalsDto Totals,
    MockSignalDto? MockSignal,
    IReadOnlyList<RecentRecordDto> Recent);

public sealed record TrendDto(TrendLabel Label, double? ProbabilityUp, double? Difference);

public sealed record TotalsDto(int Records, int Questions, int Correct, int Wrong, int Blank);

/// <param name="Exams">Fully tagged mock exams that asked the topic's subject.</param>
public sealed record MockSignalDto(double Exposure, int Missed, int Wrong, int Blank, int Exams, double Rate, double SubjectRate, bool Shown);

public sealed record RecentRecordDto(DateOnly Date, int Total, int Correct, int Wrong, int Blank, bool IsRepeat);

/// <summary>M11a form + M11b gap. Accuracy and net intervals are 80 %.</summary>
public sealed record SectionDto(
    string Code,
    int Exams,
    bool IsSufficient,
    double Accuracy,
    double AccuracyLow,
    double AccuracyHigh,
    double MarkingRate,
    double ExpectedNet,
    double ExpectedNetLow,
    double ExpectedNetHigh,
    TrendDto Trend,
    double? PracticeGap,
    bool GapWarning);
