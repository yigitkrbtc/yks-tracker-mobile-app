namespace Nirengi.Modules.Recommendation.Domain.Plans;

/// <summary>A student's frozen plan for one week (table <c>recommendation.weekly_plans</c>).</summary>
public sealed class StoredPlan
{
    public Guid UserId { get; set; }

    /// <summary>Monday of the week (Europe/Istanbul).</summary>
    public DateOnly WeekStart { get; set; }

    public string RuleSetVersion { get; set; } = null!;

    /// <summary>The analysis stamp the plan was last built from; a thin plan is rebuilt when it moves.</summary>
    public string AnalysisStamp { get; set; } = null!;

    /// <summary>JSON array of <see cref="PlanItem"/>.</summary>
    public string Items { get; set; } = null!;

    public DateTimeOffset GeneratedAt { get; set; }
}

public enum DismissReason
{
    NotNow,
    AlreadyKnow,
}

/// <summary>An item the student dismissed (table <c>recommendation.item_feedback</c>); the beta objection rate.</summary>
public sealed class ItemFeedback
{
    public Guid UserId { get; set; }

    public DateOnly WeekStart { get; set; }

    public string ItemKey { get; set; } = null!;

    public DismissReason Reason { get; set; }

    public DateTimeOffset At { get; set; }
}
