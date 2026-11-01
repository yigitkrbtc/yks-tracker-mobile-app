namespace Nirengi.Modules.Recommendation.Domain.Plans;

/// <summary>Live progress of an item; never stored (ADR-0017).</summary>
/// <param name="Done">Questions recorded on the item's topic this week (0 for section items).</param>
public sealed record ItemProgress(int Done, bool Completed);

public static class PlanProgress
{
    /// <param name="weekQuestions">Questions per topic recorded from the week's Monday to today.</param>
    /// <param name="taggedKeys">
    /// Keys (<c>tag:{exam}:{section}</c>) of sections that are fully tagged. A section that left the tagging window
    /// or whose exam was deleted is not in it, so its item stays open.
    /// </param>
    public static ItemProgress Of(PlanItem item, IReadOnlyDictionary<Guid, int> weekQuestions, IReadOnlySet<string> taggedKeys)
    {
        if (item.Kind == PlanItemKind.TagMockExam)
        {
            return new ItemProgress(0, taggedKeys.Contains(item.Key));
        }

        if (item.TopicId is { } topic && item.TargetQuestions is { } target)
        {
            var done = weekQuestions.GetValueOrDefault(topic);
            return new ItemProgress(done, done >= target);
        }

        return new ItemProgress(0, false);
    }

    public static string TagKey(Guid mockExamId, string sectionCode) => $"tag:{mockExamId:N}:{sectionCode}";
}
