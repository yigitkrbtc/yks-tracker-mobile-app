namespace Nirengi.Modules.Tracking.Domain.Profiles;

/// <summary>YKS is held every June; from July on, the next sitting is the following year's.</summary>
public static class ExamCalendar
{
    /// <summary>How far past the next sitting a target may be: a 9th grader's regular exam is three later.</summary>
    public const int MaxYearsAhead = 3;

    // Türkiye is UTC+3 all year (no DST since 2016).
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    public static int NextExamYear(DateTimeOffset utcNow)
    {
        var local = utcNow.ToOffset(TurkeyOffset);
        return local.Month <= 6 ? local.Year : local.Year + 1;
    }
}
