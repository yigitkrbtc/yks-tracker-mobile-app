using Nirengi.Modules.Curriculum.Domain.Calendar;

namespace Nirengi.Modules.Curriculum.Application.Abstractions;

/// <param name="Version">Changes whenever any calendar changes; the endpoint's ETag.</param>
public sealed record ExamCalendarSet(string Version, IReadOnlyList<ExamCalendar> Calendars);

/// <summary>The published exam calendars (seeded from data/seed/exam_calendar.json, editable from the admin panel).</summary>
public interface IExamCalendarSource
{
    Task<ExamCalendarSet> Get(CancellationToken cancellationToken);
}
