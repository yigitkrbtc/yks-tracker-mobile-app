namespace Nirengi.Modules.Recommendation.Domain.Plans;

public enum TopicConfidence
{
    None,
    VeryLow,
    Preliminary,
    Reasonable,
    Reliable,
}

public enum TopicTrend
{
    None,
    Insufficient,
    Improving,
    Stable,
    Declining,
}

/// <summary>The student's own mark on a topic. Changes the wording, never the choice.</summary>
public enum DeclaredStatus
{
    Unknown,
    NotStarted,
    InProgress,
    Done,
}

/// <summary>A topic of the student's active tests. Topics outside the dictionary are never recommended.</summary>
/// <param name="ExpectedQuestions">q_i: expected questions in the exam.</param>
public sealed record TopicInfo(Guid TopicId, string Code, string Name, string SubjectCode, double ExpectedQuestions);

/// <summary>A topic with analysis data (M1–M3).</summary>
/// <param name="Questions">All recorded questions on the topic.</param>
public sealed record TopicState(
    Guid TopicId,
    double? Mastery,
    double? MasteryLow,
    double? MasteryHigh,
    TopicConfidence Confidence,
    TopicTrend Trend,
    double? ProbabilityUp,
    int? QuestionsToNextLevel,
    int Questions,
    DateOnly? LastStudiedOn);

/// <summary>An M11b practice/mock gap warning of a mock section.</summary>
public sealed record SectionGap(string Code, string Name, double Gap);

/// <summary>A mock exam section with untagged wrong and blank answers.</summary>
public sealed record PendingTag(Guid MockExamId, DateOnly TakenOn, string SectionCode, string SectionName, int WrongAndBlank);

/// <summary>Everything one plan is built from. Pure data; the engine does no I/O and reads no clock (ADR-0017).</summary>
/// <param name="Today">Reference day, Europe/Istanbul.</param>
/// <param name="Priorities">M6 order (topic ids), already capped by the analysis.</param>
/// <param name="LowDataTopics">Heavy topics with too little data, in priority order.</param>
/// <param name="PendingTagging">Newest exam first.</param>
/// <param name="Dismissed">Item keys the student dismissed this week.</param>
public sealed record RecommendationInput(
    DateOnly Today,
    IReadOnlyDictionary<Guid, TopicInfo> Topics,
    IReadOnlyList<Guid> Priorities,
    IReadOnlyList<Guid> LowDataTopics,
    IReadOnlyList<TopicState> States,
    IReadOnlyList<SectionGap> Gaps,
    IReadOnlyList<PendingTag> PendingTagging,
    IReadOnlyDictionary<Guid, DeclaredStatus> Declared,
    IReadOnlySet<string> Dismissed);
