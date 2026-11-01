using Nirengi.Modules.Curriculum.Domain.Calendar;

namespace Nirengi.Modules.Curriculum.Application.Abstractions;

/// <summary>The shipped exam calendars; read-only, changes only on deploy.</summary>
public interface IExamCalendarSource
{
    /// <summary>Content hash of the calendar file; doubles as the ETag.</summary>
    string Version { get; }

    IReadOnlyList<ExamCalendar> Calendars { get; }
}
