namespace Nirengi.Modules.Curriculum.Domain.Calendar;

/// <summary>An exam year's calendar as stored (ADR-0019: editable from the admin panel); <see cref="Data"/> is its JSON.</summary>
public sealed class StoredExamCalendar
{
    private StoredExamCalendar()
    {
    }

    public int ExamYear { get; private set; }

    public string Data { get; private set; } = null!;

    public static StoredExamCalendar Create(int examYear, string data) => new() { ExamYear = examYear, Data = data };

    public void Replace(string data) => Data = data;
}
