namespace Nirengi.Modules.Analytics.Engine;

/// <summary>M2 confidence label. <see cref="None"/>: no record at all (grey, not "failed").</summary>
public enum ConfidenceLevel
{
    None,
    VeryLow,
    Preliminary,
    Reasonable,
    Reliable,
}

/// <summary>Weak/strong verdict; withheld (<see cref="Insufficient"/>) before enough questions and records.</summary>
public enum MasteryBand
{
    None,
    Insufficient,
    Weak,
    Developing,
    Strong,
}

public enum TrendLabel
{
    None,
    Insufficient,
    Improving,
    Stable,
    Declining,
}

public sealed record AnalysisResult(
    string RuleSetVersion,
    int InvalidRecords,
    IReadOnlyDictionary<Guid, TopicAnalysis> Topics,
    IReadOnlyDictionary<string, AggregateAnalysis> Subjects,
    IReadOnlyDictionary<string, AggregateAnalysis> Tests,
    IReadOnlyDictionary<string, SectionAnalysis> Sections,
    IReadOnlyList<Guid> Priorities,
    IReadOnlyList<Guid> LowDataTopics);

/// <summary>
/// M1–M3 (+ M6, M11c) for one topic. Numbers are null when the topic has no record; the interval is the
/// 90 % credible interval of accuracy.
/// </summary>
public sealed record TopicAnalysis(
    double? Mastery,
    double? MasteryLow,
    double? MasteryHigh,
    double? MarkingRate,
    bool Optimistic,
    double? ExpectedNetPerQuestion,
    double? EffectiveQuestions,
    double? Dispersion,
    double? Confidence,
    ConfidenceLevel ConfidenceLevel,
    MasteryBand Band,
    TrendResult Trend,
    double? Priority,
    TopicTotals Totals,
    MockSignal? MockSignal)
{
    public static TopicAnalysis Empty { get; } = new(
        null, null, null, null, false, null, null, null, null, ConfidenceLevel.None, MasteryBand.None,
        TrendResult.NoData, null, TopicTotals.Zero, null);

    public bool HasData => Totals.Records > 0;
}

/// <param name="ProbabilityUp">P(recent accuracy &gt; previous accuracy); null when the trend is not computed.</param>
/// <param name="Difference">Recent minus previous posterior mean.</param>
public sealed record TrendResult(TrendLabel Label, double? ProbabilityUp, double? Difference)
{
    public static TrendResult NoData { get; } = new(TrendLabel.None, null, null);

    public static TrendResult Insufficient { get; } = new(TrendLabel.Insufficient, null, null);
}

/// <summary>All-time raw totals (no decay, no repeat weighting), shown next to the decayed metrics.</summary>
public sealed record TopicTotals(int Records, int Questions, int Correct, int Wrong, int Blank)
{
    public static TopicTotals Zero { get; } = new(0, 0, 0, 0, 0);
}

/// <summary>M11c: wrong and blank answers tagged to the topic in fully tagged mock sections.</summary>
/// <param name="Exposure">E_i: expected questions of the topic across those sections.</param>
/// <param name="Missed">M_i: tagged wrong + blank.</param>
/// <param name="Exams">Mock exams that contributed exposure.</param>
/// <param name="Rate">Posterior mean miss rate of the topic.</param>
/// <param name="SubjectRate">Posterior mean miss rate of the topic's subject.</param>
/// <param name="Shown">Exposure is large enough to show the signal and add it to the priority.</param>
public sealed record MockSignal(
    double Exposure,
    int Missed,
    int Wrong,
    int Blank,
    int Exams,
    double Rate,
    double SubjectRate,
    bool Shown);

/// <summary>M7 lite: q-weighted mastery of a subject or test; unstudied topics enter with their prior.</summary>
/// <param name="Coverage">Share of q whose topic confidence exceeds 0.5.</param>
public sealed record AggregateAnalysis(
    double Mastery,
    double MasteryLow,
    double MasteryHigh,
    double Coverage,
    int StudiedTopics,
    int TopicCount);

/// <summary>M11a form of one mock section (+ M11b practice gap). The interval is 80 %.</summary>
public sealed record SectionAnalysis(
    int Exams,
    bool IsSufficient,
    double Accuracy,
    double AccuracyLow,
    double AccuracyHigh,
    double MarkingRate,
    double ExpectedNet,
    double ExpectedNetLow,
    double ExpectedNetHigh,
    TrendResult Trend,
    double? PracticeGap,
    bool GapWarning);
