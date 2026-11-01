using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nirengi.Modules.Curriculum.Application.Abstractions;
using Nirengi.Modules.Curriculum.Domain.Calendar;

namespace Nirengi.Modules.Curriculum.Infrastructure.Seed;

/// <summary>
/// data/seed/exam_calendar.json compiled into the assembly and parsed once. Parsing validates codes and date
/// order, so a broken file fails the unit tests and startup, never a request.
/// </summary>
internal sealed class ExamCalendarSeed : IExamCalendarSource
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private ExamCalendarSeed(string version, IReadOnlyList<ExamCalendar> calendars)
    {
        Version = version;
        Calendars = calendars;
    }

    public string Version { get; }

    public IReadOnlyList<ExamCalendar> Calendars { get; }

    public static ExamCalendarSeed LoadEmbedded()
    {
        using var stream = typeof(ExamCalendarSeed).Assembly.GetManifestResourceStream("Seed.exam_calendar.json")
            ?? throw new InvalidOperationException("Embedded resource Seed.exam_calendar.json is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    public static ExamCalendarSeed Parse(string json)
    {
        var file = JsonSerializer.Deserialize<CalendarFile>(json, Json) ?? throw new FormatException("exam_calendar.json is empty.");
        if (file.Calendars.Select(c => c.ExamYear).Distinct().Count() != file.Calendars.Count)
        {
            throw new FormatException("An exam year appears twice in exam_calendar.json.");
        }

        foreach (var calendar in file.Calendars)
        {
            Validate(calendar);
        }

        var version = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..32];
        return new ExamCalendarSeed(version, file.Calendars);
    }

    private static void Validate(ExamCalendar calendar)
    {
        var at = $"exam year {calendar.ExamYear}";
        if (string.IsNullOrWhiteSpace(calendar.SourceUrl) || calendar.Events.Count == 0)
        {
            throw new FormatException($"{at}: source URL and events are required.");
        }

        var byCode = new Dictionary<string, ExamCalendarEvent>(StringComparer.Ordinal);
        foreach (var e in calendar.Events)
        {
            if (!ExamCalendar.KnownCodes.Contains(e.Code) || !byCode.TryAdd(e.Code, e))
            {
                throw new FormatException($"{at}: unknown or repeated event code '{e.Code}'.");
            }

            if (e.EndsAt is { } end && end < e.StartsAt)
            {
                throw new FormatException($"{at}: event '{e.Code}' ends before it starts.");
            }
        }

        if (!byCode.TryGetValue("tyt", out var tyt) || tyt.StartsAt.Year != calendar.ExamYear)
        {
            throw new FormatException($"{at}: the TYT date is required and must fall in the exam year.");
        }

        // Applications close before TYT; AYT and YDT follow TYT; results come last.
        Require(byCode, "application_end", e => e.StartsAt < tyt.StartsAt, at);
        Require(byCode, "application_start", e => e.StartsAt < tyt.StartsAt, at);
        Require(byCode, "ayt", e => e.StartsAt >= tyt.StartsAt, at);
        Require(byCode, "ydt", e => e.StartsAt >= tyt.StartsAt, at);
        Require(byCode, "results", e => calendar.Events.All(x => x.Code == "results" || x.StartsAt < e.StartsAt), at);
    }

    private static void Require(Dictionary<string, ExamCalendarEvent> byCode, string code, Func<ExamCalendarEvent, bool> rule, string at)
    {
        if (byCode.TryGetValue(code, out var e) && !rule(e))
        {
            throw new FormatException($"{at}: event '{code}' is out of order.");
        }
    }

    private sealed record CalendarFile(IReadOnlyList<ExamCalendar> Calendars);
}
