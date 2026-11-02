using Nirengi.Modules.Curriculum.Domain.Calendar;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Versions;
using Shouldly;

namespace Nirengi.Curriculum.UnitTests;

/// <summary>ADR-0019: drafts keep the seed parser's rules (permanent codes, grades, active topic per subject).</summary>
public sealed class DraftApplierTests
{
    private static readonly HashSet<string> Subjects = new(StringComparer.Ordinal) { "TYT.MAT", "TYT.TUR" };

    private static readonly CurriculumContent Live = new(
        [
            Topic("TYT.MAT.01", "TYT.MAT", 0),
            Topic("TYT.MAT.02", "TYT.MAT", 1),
            Topic("TYT.MAT.03", "TYT.MAT", 2),
            Topic("TYT.TUR.01", "TYT.TUR", 0),
        ],
        [new SourceState("KITAP", "Kitap", SourceKind.Publisher, 0, true)],
        []);

    [Fact]
    public void New_topic_gets_the_next_free_code_including_retired_codes()
    {
        var known = new HashSet<string>(StringComparer.Ordinal) { "TYT.MAT.07" }; // retired long ago

        var applied = Apply([Add(null, "TYT.MAT")], known);

        applied.IsSuccess.ShouldBeTrue();
        var op = applied.Value.Operations.ShouldHaveSingleItem().ShouldBeOfType<AddTopic>();
        op.Code.ShouldBe("TYT.MAT.08");
        var topic = applied.Value.Content.Topics.Single(t => t.Code == "TYT.MAT.08");
        topic.SortOrder.ShouldBe(3);
        topic.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void A_used_code_is_never_given_again()
    {
        var known = new HashSet<string>(StringComparer.Ordinal) { "TYT.MAT.09" };

        var applied = Apply([Add("TYT.MAT.09", "TYT.MAT")], known);

        applied.IsFailure.ShouldBeTrue();
        applied.Error.Message.ShouldStartWith("1. işlem:");
        applied.Error.Message.ShouldContain("tekrar kullanılmaz");
    }

    [Theory]
    [InlineData("TYT.TUR.04")] // other subject
    [InlineData("TYT.MAT.4")]  // one digit
    [InlineData("tyt.mat.04")]
    public void Code_must_match_its_subject(string code) =>
        Apply([Add(code, "TYT.MAT")]).IsFailure.ShouldBeTrue();

    [Fact]
    public void Unknown_subject_is_rejected() =>
        Apply([Add(null, "AYT.FIZ")]).Error.Message.ShouldContain("bilinmeyen ders");

    [Fact]
    public void Grades_outside_9_to_12_are_rejected() =>
        Apply([new AddTopic(null, "TYT.MAT", "Yeni", ["Alt"], 8, 10, null)]).IsFailure.ShouldBeTrue();

    [Fact]
    public void Update_keeps_position_unless_given()
    {
        var applied = Apply([new UpdateTopic("TYT.MAT.02", "  Yeni ad ", [" A ", "B"], 10, 11, " ")]);

        var topic = applied.Value.Content.Topics.Single(t => t.Code == "TYT.MAT.02");
        topic.Name.ShouldBe("Yeni ad");
        topic.Subtopics.ShouldBe(["A", "B"]);
        topic.Description.ShouldBeNull();
        topic.SortOrder.ShouldBe(1);
    }

    [Fact]
    public void Merge_retires_the_sources_and_links_them_to_the_target()
    {
        var applied = Apply([new MergeTopics(["TYT.MAT.01", "TYT.MAT.02"], "TYT.MAT.03")]);

        applied.IsSuccess.ShouldBeTrue();
        applied.Value.Content.Topics.Where(t => !t.IsActive).Select(t => t.Code).ShouldBe(["TYT.MAT.01", "TYT.MAT.02"]);
        applied.Value.Lineage.ShouldBe(
        [
            new LineageLink("TYT.MAT.01", "TYT.MAT.03", LineageRelation.Merged),
            new LineageLink("TYT.MAT.02", "TYT.MAT.03", LineageRelation.Merged),
        ]);
    }

    [Fact]
    public void Merge_across_subjects_is_rejected() =>
        Apply([new MergeTopics(["TYT.TUR.01"], "TYT.MAT.03")]).Error.Message.ShouldContain("aynı dersten");

    [Fact]
    public void Split_needs_two_active_targets_created_earlier_in_the_draft()
    {
        var applied = Apply(
        [
            Add(null, "TYT.MAT"),
            Add(null, "TYT.MAT"),
            new SplitTopic("TYT.MAT.01", ["TYT.MAT.04", "TYT.MAT.05"]),
        ]);

        applied.IsSuccess.ShouldBeTrue();
        applied.Value.Content.Topics.Single(t => t.Code == "TYT.MAT.01").IsActive.ShouldBeFalse();
        applied.Value.Lineage.ShouldAllBe(l => l.Relation == LineageRelation.Split && l.FromCode == "TYT.MAT.01");

        Apply([Add(null, "TYT.MAT"), new SplitTopic("TYT.MAT.01", ["TYT.MAT.04"])]).Error.Message.ShouldContain("en az iki");
    }

    [Fact]
    public void A_subject_cannot_lose_its_last_active_topic() =>
        Apply([new RetireTopic("TYT.TUR.01")]).Error.Message.ShouldContain("TYT.TUR dersinde aktif konu kalmıyor");

    [Fact]
    public void Retire_twice_and_reactivate_active_are_rejected()
    {
        Apply([new RetireTopic("TYT.MAT.01"), new RetireTopic("TYT.MAT.01")]).Error.Message.ShouldStartWith("2. işlem:");
        Apply([new ReactivateTopic("TYT.MAT.01")]).Error.Message.ShouldContain("zaten aktif");
    }

    [Fact]
    public void Sources_are_added_last_and_codes_are_unique()
    {
        var applied = Apply([new AddSource("YAYIN_A", "Yayın A", SourceKind.Publisher)]);
        applied.Value.Content.Sources[^1].ShouldBe(new SourceState("YAYIN_A", "Yayın A", SourceKind.Publisher, 1, true));

        Apply([new AddSource("KITAP", "Başka", SourceKind.Publisher)]).IsFailure.ShouldBeTrue();
        Apply([new AddSource("küçük", "Ad", SourceKind.Publisher)]).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Calendar_is_validated_and_replaced_per_year()
    {
        var valid = Calendar(2027, "Kesin tarih");
        var applied = Apply([new SetCalendar(valid), new SetCalendar(valid with { Note = "Güncel" })]);

        applied.Value.Content.Calendars.ShouldHaveSingleItem().Note.ShouldBe("Güncel");
        Apply([new SetCalendar(valid with { Events = [] })]).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Too_many_operations_are_rejected() =>
        Apply([.. Enumerable.Range(0, DraftApplier.MaxOperations + 1).Select(_ => new UpdateTopic("TYT.MAT.01", "Ad", ["Alt"], 9, 9, null))])
            .IsFailure.ShouldBeTrue();

    internal static TopicState Topic(string code, string subject, int sort, bool active = true) =>
        new(code, subject, $"Konu {code}", ["Alt konu"], 9, 10, null, sort, active);

    internal static ExamCalendar Calendar(int year, string? note) => new(
        year,
        "https://www.osym.gov.tr/",
        note,
        [new ExamCalendarEvent("tyt", "TYT", new DateTimeOffset(year, 6, 19, 10, 15, 0, TimeSpan.FromHours(3)), null, CalendarEventStatus.Announced)]);

    private static AddTopic Add(string? code, string subject) => new(code, subject, "Yeni konu", ["Alt konu"], 9, 10, null);

    private static Nirengi.BuildingBlocks.Domain.Results.Result<AppliedDraft> Apply(IReadOnlyList<CurriculumOperation> ops, IReadOnlySet<string>? known = null) =>
        DraftApplier.Apply(Live, Subjects, known ?? new HashSet<string>(StringComparer.Ordinal), ops);
}
