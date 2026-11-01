namespace Nirengi.Modules.Recommendation.Domain.Plans;

/// <summary>
/// Rule parameters (ADR-0017). Any change bumps <see cref="Version"/>, which makes stored plans of the current
/// week rebuild on the next read.
/// </summary>
public sealed record RecommendationRuleSet(
    string Version,
    int MaxItems,
    int MaxPerSubject,
    int TagWindowDays,
    double ReviewMastery,
    int ReviewAfterDays,
    int MinTarget,
    int MaxTarget,
    int CollectTarget,
    int DeclineTarget,
    int ReviewTarget,
    int StarterTopics)
{
    public static readonly RecommendationRuleSet V1 = new(
        Version: "R2026.1",
        MaxItems: 5,
        MaxPerSubject: 2,
        TagWindowDays: 14,
        ReviewMastery: 0.80,
        ReviewAfterDays: 21,
        MinTarget: 20,
        MaxTarget: 60,
        CollectTarget: 20,
        DeclineTarget: 20,
        ReviewTarget: 15,
        StarterTopics: 3);

    /// <summary>Questions to the next confidence level, rounded up to tens and clamped.</summary>
    public int FocusTarget(int? questionsToNextLevel)
    {
        var raw = questionsToNextLevel ?? MinTarget;
        var rounded = (int)Math.Ceiling(raw / 10.0) * 10;
        return Math.Clamp(rounded, MinTarget, MaxTarget);
    }
}
