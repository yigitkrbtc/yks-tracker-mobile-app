using Nirengi.Modules.Curriculum.Domain.Calendar;
using Nirengi.Modules.Curriculum.Infrastructure.Seed;
using Shouldly;

namespace Nirengi.Curriculum.UnitTests;

/// <summary>Guards data/seed/exam_calendar.json: a broken file fails here, not at startup.</summary>
public sealed class ExamCalendarSeedTests
{
    [Fact]
    public void Shipped_calendar_covers_the_seed_exam_year()
    {
        var seed = ExamCalendarSeed.LoadEmbedded();
        var year = CurriculumSeed.LoadEmbedded().ExamYear;

        var calendar = seed.Calendars.Single(c => c.ExamYear == year);
        calendar.Events.Single(e => e.Code == "tyt").StartsAt.Year.ShouldBe(year);
        calendar.Events.ShouldContain(e => e.Code == "ayt");
        seed.Version.Length.ShouldBe(32);
    }

    [Fact]
    public void Parses_status_and_offsets()
    {
        var seed = ExamCalendarSeed.Parse(Calendar(("tyt", "2027-06-19T10:15:00+03:00", "announced")));

        var tyt = seed.Calendars[0].Events[0];
        tyt.Status.ShouldBe(CalendarEventStatus.Announced);
        tyt.StartsAt.ShouldBe(new DateTimeOffset(2027, 6, 19, 7, 15, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("ayt", "2027-06-18T10:15:00+03:00")]
    [InlineData("application_end", "2027-07-01T23:59:00+03:00")]
    [InlineData("results", "2027-06-01T10:00:00+03:00")]
    public void Rejects_events_out_of_order(string code, string at) =>
        Should.Throw<FormatException>(() => ExamCalendarSeed.Parse(Calendar(("tyt", "2027-06-19T10:15:00+03:00", "estimated"), (code, at, "estimated"))));

    [Fact]
    public void Rejects_unknown_codes_and_missing_tyt()
    {
        Should.Throw<FormatException>(() => ExamCalendarSeed.Parse(Calendar(("tyt", "2027-06-19T10:15:00+03:00", "estimated"), ("lgs", "2027-06-01T10:00:00+03:00", "estimated"))));
        Should.Throw<FormatException>(() => ExamCalendarSeed.Parse(Calendar(("ayt", "2027-06-20T10:15:00+03:00", "estimated"))));
        Should.Throw<FormatException>(() => ExamCalendarSeed.Parse(Calendar(("tyt", "2026-06-20T10:15:00+03:00", "estimated"))));
    }

    private static string Calendar(params (string Code, string At, string Status)[] events) =>
        $$"""
        { "calendars": [ { "examYear": 2027, "sourceUrl": "https://www.osym.gov.tr", "events": [
          {{string.Join(",", events.Select(e => $$"""{ "code": "{{e.Code}}", "name": "{{e.Code}}", "startsAt": "{{e.At}}", "status": "{{e.Status}}" }"""))}}
        ] } ] }
        """;
}
