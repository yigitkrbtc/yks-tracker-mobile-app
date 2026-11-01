namespace Nirengi.Modules.Analytics.Contracts;

/// <summary>M2 confidence label as other modules see it.</summary>
public enum DigestConfidence
{
    None,
    VeryLow,
    Preliminary,
    Reasonable,
    Reliable,
}

/// <summary>M3 trend label as other modules see it.</summary>
public enum DigestTrend
{
    None,
    Insufficient,
    Improving,
    Stable,
    Declining,
}

/// <summary>An M6 priority entry, in priority order.</summary>
public sealed record DigestPriority(Guid TopicId, double Priority, double Mastery, double ExpectedQuestions);

/// <summary>A topic the student has data for.</summary>
/// <param name="ProbabilityUp">P(recent accuracy &gt; previous); null when no trend was computed.</param>
/// <param name="QuestionsToNextLevel">Fresh questions that lift the confidence one level; null at the top.</param>
/// <param name="Questions">All recorded questions on the topic (raw total).</param>
/// <param name="ExpectedQuestions">q_i: expected questions in the exam.</param>
public sealed record DigestTopic(
    Guid TopicId,
    double? Mastery,
    double? MasteryLow,
    double? MasteryHigh,
    DigestConfidence Confidence,
    DigestTrend Trend,
    double? ProbabilityUp,
    int? QuestionsToNextLevel,
    int Questions,
    DateOnly? LastStudiedOn,
    double ExpectedQuestions);

/// <summary>M11a/b of a mock section.</summary>
public sealed record DigestSection(string Code, bool IsSufficient, double? PracticeGap, bool GapWarning);

/// <summary>What the recommendation engine needs from a student's analysis (ADR-0017).</summary>
/// <param name="Stamp">Changes whenever any value here may change (the analysis ETag).</param>
/// <param name="LowDataTopics">Heavy topics with too little data, in priority order.</param>
public sealed record AnalysisDigest(
    string Stamp,
    DateOnly ComputedOn,
    string RuleSetVersion,
    IReadOnlyList<DigestPriority> Priorities,
    IReadOnlyList<Guid> LowDataTopics,
    IReadOnlyList<DigestTopic> Topics,
    IReadOnlyList<DigestSection> Sections);

/// <summary>Read access to a student's current analysis, refreshed first when stale.</summary>
public interface IAnalysisDigestReader
{
    /// <summary>Null when the curriculum is not loaded yet.</summary>
    Task<AnalysisDigest?> Get(Guid userId, CancellationToken cancellationToken);
}
