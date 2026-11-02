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

    public const int SourceUrlMaxLength = 300;

    public const int NoteMaxLength = 500;

    public const int EventNameMaxLength = 60;

    /// <summary>What is wrong with this calendar, or null when it is valid (seed file and admin edits alike).</summary>
    public string? Problem()
    {
        var at = $"{ExamYear} takvimi";
        if (string.IsNullOrWhiteSpace(SourceUrl) || SourceUrl.Length > SourceUrlMaxLength || Events.Count == 0)
        {
            return $"{at}: kaynak adresi ve en az bir tarih gerekli.";
        }

        if (Note is { Length: > NoteMaxLength })
        {
            return $"{at}: not en çok {NoteMaxLength} karakter olabilir.";
        }

        var byCode = new Dictionary<string, ExamCalendarEvent>(StringComparer.Ordinal);
        foreach (var e in Events)
        {
            if (!KnownCodes.Contains(e.Code) || !byCode.TryAdd(e.Code, e))
            {
                return $"{at}: bilinmeyen veya tekrarlanan tarih kodu '{e.Code}'.";
            }

            if (string.IsNullOrWhiteSpace(e.Name) || e.Name.Length > EventNameMaxLength)
            {
                return $"{at}: '{e.Code}' tarihinin adı boş veya çok uzun.";
            }

            if (e.EndsAt is { } end && end < e.StartsAt)
            {
                return $"{at}: '{e.Code}' başlamadan bitiyor.";
            }
        }

        if (!byCode.TryGetValue("tyt", out var tyt) || tyt.StartsAt.Year != ExamYear)
        {
            return $"{at}: TYT tarihi gerekli ve sınav yılında olmalı.";
        }

        // Applications close before TYT; AYT and YDT follow TYT; results come last.
        var outOfOrder = new (string Code, Func<ExamCalendarEvent, bool> Rule)[]
        {
            ("application_end", e => e.StartsAt < tyt.StartsAt),
            ("application_start", e => e.StartsAt < tyt.StartsAt),
            ("ayt", e => e.StartsAt >= tyt.StartsAt),
            ("ydt", e => e.StartsAt >= tyt.StartsAt),
            ("results", e => Events.All(x => x.Code == "results" || x.StartsAt < e.StartsAt)),
        }.FirstOrDefault(r => byCode.TryGetValue(r.Code, out var e) && !r.Rule(e));
        return outOfOrder.Code is null ? null : $"{at}: '{outOfOrder.Code}' tarihi sırası yanlış.";
    }
}
