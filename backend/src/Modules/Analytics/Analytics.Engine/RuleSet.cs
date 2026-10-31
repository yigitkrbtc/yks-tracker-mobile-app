namespace Nirengi.Modules.Analytics.Engine;

/// <summary>
/// Versioned parameters of the metric specification. Every snapshot stores <see cref="Version"/>; any change here is
/// a new version and must pass the golden dataset (ADR-0003, ADR-0016). Values marked [Sezgisel] in the spec are
/// design choices to be calibrated with real data.
/// </summary>
public sealed record RuleSet
{
    public static RuleSet V1 { get; } = new();

    public string Version { get; init; } = "2026.1";

    // M1: decay and prior.
    public double HalfLifeDays { get; init; } = 30;

    public double RepeatWeight { get; init; } = 0.5;

    public double Interval { get; init; } = 0.90;

    public double PriorStrength { get; init; } = 4;

    public int PriorMinMarked { get; init; } = 20;

    public double PriorMeanMin { get; init; } = 0.125;

    public double PriorMeanMax { get; init; } = 0.875;

    public double OptimisticMarking { get; init; } = 0.70;

    // M2: confidence.
    public double ConfidenceK { get; init; } = 20;

    public double FreshnessHalfLifeDays { get; init; } = 40;

    public double DispersionMin { get; init; } = 1;

    public double DispersionMax { get; init; } = 3;

    public double PreliminaryFrom { get; init; } = 0.35;

    public double ReasonableFrom { get; init; } = 0.60;

    public double ReliableFrom { get; init; } = 0.80;

    public int BandMinQuestions { get; init; } = 20;

    public int BandMinRecords { get; init; } = 2;

    public double WeakBelow { get; init; } = 0.50;

    public double StrongFrom { get; init; } = 0.80;

    // M3: trend.
    public int TrendRecentDays { get; init; } = 21;

    public int TrendPreviousDays { get; init; } = 60;

    public int TrendMinRecords { get; init; } = 4;

    public int TrendMinQuestions { get; init; } = 60;

    public int TrendMinSpanDays { get; init; } = 14;

    public int TrendMinRecentQuestions { get; init; } = 15;

    public int TrendMinRecentRecords { get; init; } = 2;

    public int TrendMinPreviousRecords { get; init; } = 2;

    public double TrendUpProbability { get; init; } = 0.85;

    public double TrendDownProbability { get; init; } = 0.90;

    public double TrendMinDifference { get; init; } = 0.05;

    // M6: priority (no FSRS term until M5, so the review weight is zero).
    public double TargetMastery { get; init; } = 0.80;

    public double ReviewWeight { get; init; }

    public double MockSignalWeight { get; init; } = 0.5;

    public int PriorityTop { get; init; } = 5;

    public int PriorityPerSubject { get; init; } = 2;

    public int PriorityLowData { get; init; } = 2;

    // M7 lite.
    public double AggregateZ { get; init; } = 1.6448536269514722;

    public double CoverageConfidence { get; init; } = 0.5;

    // M11.
    public double MockInterval { get; init; } = 0.80;

    public int MockMinExams { get; init; } = 3;

    public int MockTrendRecent { get; init; } = 3;

    public int MockTrendPrevious { get; init; } = 6;

    public int MockTrendMinPrevious { get; init; } = 2;

    public int MockTrendMinAgreeing { get; init; } = 2;

    public double GapThreshold { get; init; } = 0.15;

    public int GapMinTopics { get; init; } = 3;

    public int SignalLookbackExams { get; init; } = 10;

    public double SignalMinExposure { get; init; } = 3;

    /// <summary>Decay weight of a record <paramref name="ageDays"/> old.</summary>
    public double Decay(int ageDays) => Math.Pow(0.5, ageDays / HalfLifeDays);

    /// <summary>p_gelişim of M6.</summary>
    public static double Learnability(double mastery) => mastery switch
    {
        < 0.5 => 0.6,
        > 0.9 => 0.3,
        _ => 1.0,
    };

    internal TrendLabel Classify(double probabilityUp, double difference)
    {
        if (probabilityUp >= TrendUpProbability && difference >= TrendMinDifference)
        {
            return TrendLabel.Improving;
        }

        if (1 - probabilityUp >= TrendDownProbability && -difference >= TrendMinDifference)
        {
            return TrendLabel.Declining;
        }

        return TrendLabel.Stable;
    }
}
