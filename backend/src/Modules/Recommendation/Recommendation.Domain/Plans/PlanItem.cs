namespace Nirengi.Modules.Recommendation.Domain.Plans;

/// <summary>Item kinds in priority order: a lower value wins a slot and a topic first.</summary>
public enum PlanItemKind
{
    TagMockExam,
    Focus,
    Decline,
    CollectData,
    Review,
    PracticeGap,
    FirstRecord,
}

/// <summary>
/// One thing to do this week. <see cref="Key"/> is stable for the same kind and target, so a dismissal or a
/// completed item is recognised across rebuilds. Structured fields let a later AI writer (v1.1) fill the same
/// shape; <see cref="Text"/> follows evidence → reason → expected benefit → confidence.
/// </summary>
/// <param name="SubjectCode">Subject the item counts against for the per-subject cap; null for section items.</param>
/// <param name="TargetQuestions">Questions to record on the topic this week for the item to count as done.</param>
public sealed record PlanItem(
    string Key,
    PlanItemKind Kind,
    Guid? TopicId,
    string? SubjectCode,
    string? SectionCode,
    Guid? MockExamId,
    int? TargetQuestions,
    string Title,
    string Text);

/// <summary>A week's plan. The week starts Monday 00:00 Europe/Istanbul.</summary>
public sealed record WeeklyPlan(DateOnly WeekStart, string RuleSetVersion, IReadOnlyList<PlanItem> Items);

public static class Week
{
    public static DateOnly StartOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
