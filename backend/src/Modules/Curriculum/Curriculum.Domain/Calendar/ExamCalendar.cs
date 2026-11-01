namespace Nirengi.Modules.Curriculum.Domain.Calendar;

/// <summary>Whether ÖSYM announced the date or it is projected from earlier years.</summary>
public enum CalendarEventStatus
{
    Announced,
    Estimated,
}

/// <summary>A dated step of an exam year (a session, an application window, results).</summary>
/// <param name="Code">Stable key: <c>tyt</c>, <c>ayt</c>, <c>ydt</c>, <c>application_start</c>, <c>application_end</c>, <c>late_application</c>, <c>results</c>.</param>
public sealed record ExamCalendarEvent(string Code, string Name, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, CalendarEventStatus Status);

/// <summary>The YKS calendar of one exam year (data/seed/exam_calendar.json, ADR-0018).</summary>
public sealed record ExamCalendar(int ExamYear, string SourceUrl, string? Note, IReadOnlyList<ExamCalendarEvent> Events)
{
    public static readonly IReadOnlySet<string> KnownCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "application_start", "application_end", "late_application", "tyt", "ayt", "ydt", "results",
    };
}
