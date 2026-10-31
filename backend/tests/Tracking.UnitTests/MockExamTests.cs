using Nirengi.Modules.Tracking.Domain;
using Nirengi.Modules.Tracking.Domain.MockExams;
using Shouldly;

namespace Nirengi.Tracking.UnitTests;

public sealed class MockExamTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.CreateVersion7();
    private static readonly Guid History = Guid.NewGuid();
    private static readonly Guid Geography = Guid.NewGuid();
    private static readonly Guid Maths = Guid.NewGuid();

    private static readonly Dictionary<string, MockSectionDefinition> Template = new[]
    {
        new MockSectionDefinition("TYT-TUR", "TYT-TUR", "TYT", ["TYT.TUR"], 40, null, null),
        new MockSectionDefinition("TYT-SOS", "TYT-SOS", "TYT", ["TYT.TAR", "TYT.COG", "TYT.FEL", "TYT.DKAB"], 20, null, null),
        new MockSectionDefinition("TYT-SOS.TAR", "TYT-SOS", "TYT", ["TYT.TAR"], 5, "TYT-SOS", null),
        new MockSectionDefinition("TYT-SOS.COG", "TYT-SOS", "TYT", ["TYT.COG"], 5, "TYT-SOS", null),
        new MockSectionDefinition("TYT-SOS.DKAB", "TYT-SOS", "TYT", ["TYT.DKAB"], 5, "TYT-SOS", null),
        new MockSectionDefinition("TYT-SOS.FEL2", "TYT-SOS", "TYT", ["TYT.FEL"], 5, "TYT-SOS", "TYT-SOS.DKAB"),
        new MockSectionDefinition("AYT-MAT", "AYT-MAT", "AYT", ["AYT.AMAT", "AYT.GEO"], 40, null, null),
    }.ToDictionary(d => d.Code);

    private static readonly Dictionary<Guid, string> Topics = new()
    {
        [History] = "TYT.TAR",
        [Geography] = "TYT.COG",
        [Maths] = "AYT.AMAT",
    };

    private static MockExamData Tyt(MockSectionData[]? sections = null, TopicMarkData[]? marks = null) => new(
        MockExamKind.Tyt,
        new DateOnly(2026, 10, 30),
        null,
        "Yayınevi",
        "Türkiye geneli 3",
        135,
        null,
        sections ?? [new("TYT-TUR", 40, 30, 8), new("TYT-SOS.TAR", 5, 3, 1), new("TYT-SOS.COG", 5, 2, 2)],
        marks ?? []);

    [Fact]
    public void Blank_and_net_follow_from_correct_and_wrong()
    {
        var exam = MockExam.Create(Guid.CreateVersion7(), User, Tyt(), Now, Now);

        var turkish = exam.Sections.Single(s => s.SectionCode == "TYT-TUR");
        turkish.Blank.ShouldBe(2);
        turkish.Net.ShouldBe(28m);
        exam.Sections.Single(s => s.SectionCode == "TYT-SOS.COG").Net.ShouldBe(1.5m);
        exam.Sections.ShouldAllBe(s => s.UserId == User && s.TakenOn == new DateOnly(2026, 10, 30) && s.TaggingStatus == TaggingStatus.None);
    }

    [Fact]
    public void Tagging_status_is_full_only_when_every_lost_question_is_marked()
    {
        // History lost 1 wrong + 1 blank; geography lost 2 wrong + 1 blank; Turkish lost 8 + 2, none marked.
        var exam = MockExam.Create(
            Guid.CreateVersion7(),
            User,
            Tyt(marks: [new("TYT-SOS.TAR", History, 1, 1), new("TYT-SOS.COG", Geography, 1, 0)]),
            Now,
            Now);

        exam.Sections.Single(s => s.SectionCode == "TYT-SOS.TAR").TaggingStatus.ShouldBe(TaggingStatus.Full);
        exam.Sections.Single(s => s.SectionCode == "TYT-SOS.COG").TaggingStatus.ShouldBe(TaggingStatus.Partial);
        exam.Sections.Single(s => s.SectionCode == "TYT-TUR").TaggingStatus.ShouldBe(TaggingStatus.None);
    }

    [Fact]
    public void A_perfect_section_counts_as_fully_tagged()
    {
        var exam = MockExam.Create(Guid.CreateVersion7(), User, Tyt([new("TYT-SOS.TAR", 5, 5, 0)]), Now, Now);

        exam.Sections.Single().TaggingStatus.ShouldBe(TaggingStatus.Full);
    }

    [Fact]
    public void Tagging_later_updates_rows_in_place_and_keeps_the_rest()
    {
        var exam = MockExam.Create(Guid.CreateVersion7(), User, Tyt(), Now.AddMinutes(-10), Now);
        var history = exam.Sections.Single(s => s.SectionCode == "TYT-SOS.TAR");

        exam.Apply(Tyt(marks: [new("TYT-SOS.TAR", History, 1, 0, MistakeAttention: 1)]), Now.AddMinutes(-5), Now).ShouldBeTrue();

        exam.Sections.Single(s => s.SectionCode == "TYT-SOS.TAR").ShouldBeSameAs(history);
        history.TaggingStatus.ShouldBe(TaggingStatus.Partial);
        exam.Marks.ShouldHaveSingleItem().MistakeAttention.ShouldBe(1);
    }

    [Fact]
    public void Older_or_replayed_edit_changes_nothing_and_delete_follows_the_same_rule()
    {
        var exam = MockExam.Create(Guid.CreateVersion7(), User, Tyt(), Now.AddMinutes(-5), Now);

        exam.Apply(Tyt([new("TYT-TUR", 40, 0, 0)]), Now.AddMinutes(-6), Now).ShouldBeFalse();
        exam.Apply(Tyt([new("TYT-TUR", 40, 0, 0)]), Now.AddMinutes(-5), Now).ShouldBeFalse();
        exam.Delete(Now.AddMinutes(-6), Now).ShouldBeFalse();
        exam.Sections.Count.ShouldBe(3);

        exam.Delete(Now.AddMinutes(-1), Now).ShouldBeTrue();
        exam.IsDeleted.ShouldBeTrue();
        exam.Sections.ShouldBeEmpty();
        exam.Delete(Now, Now).ShouldBeFalse();

        exam.Apply(Tyt(), Now, Now).ShouldBeTrue();
        exam.IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public void Future_client_time_is_capped_at_server_time()
    {
        var exam = MockExam.Create(Guid.CreateVersion7(), User, Tyt(), Now.AddDays(3), Now);

        exam.ClientModifiedAt.ShouldBe(Now);
    }

    [Fact]
    public void Guards_catch_counts_the_validator_should_have_refused()
    {
        Should.Throw<ArgumentException>(() => MockExam.Create(Guid.CreateVersion7(), User, Tyt([new("TYT-TUR", 40, 30, 11)]), Now, Now));
        Should.Throw<ArgumentException>(() => MockExam.Create(Guid.CreateVersion7(), User, Tyt(marks: [new("TYT-SOS.TAR", History, 2, 0)]), Now, Now));
        Should.Throw<ArgumentException>(() => MockExam.Create(Guid.CreateVersion7(), User, Tyt(marks: [new("TYT-MAT", Maths, 1, 0)]), Now, Now));
        Should.Throw<ArgumentException>(() => MockExam.Create(Guid.CreateVersion7(), User, Tyt([]), Now, Now));
    }

    [Fact]
    public void Template_accepts_a_valid_exam()
    {
        MockExamTemplate.Check(Tyt(marks: [new("TYT-SOS.TAR", History, 1, 1)]), Template, Topics).ShouldBeNull();
        MockExamTemplate.Check(Tyt([new("TYT-SOS", 20, 10, 4)]), Template, Topics).ShouldBeNull();
        MockExamTemplate.Check(Tyt([new("AYT-MAT", 25, 10, 4)]) with { Kind = MockExamKind.Branch }, Template, Topics).ShouldBeNull();
    }

    public static TheoryData<string, string> InvalidTemplateCases => new()
    {
        { "unknown-section", "tracking.unknown_mock_section" },
        { "ayt-in-tyt", "tracking.mock_sections_kind" },
        { "branch-two-sections", "tracking.mock_sections_kind" },
        { "tyt-own-count", "tracking.mock_section_question_count" },
        { "too-many-answers", "tracking.mock_section_counts" },
        { "block-and-subject", "tracking.mock_sections_overlap" },
        { "religion-and-alternative", "tracking.mock_sections_overlap" },
        { "marks-exceed", "tracking.marks_exceed_section" },
        { "unknown-topic", "tracking.unknown_topic" },
        { "topic-of-other-subject", "tracking.topic_not_in_section" },
    };

    [Theory]
    [MemberData(nameof(InvalidTemplateCases))]
    public void Template_rejects_exams_that_do_not_fit(string invalid, string code)
    {
        var data = invalid switch
        {
            "unknown-section" => Tyt([new("TYT-XXX", 5, 1, 1)]),
            "ayt-in-tyt" => Tyt([new("AYT-MAT", 40, 1, 1)]),
            "branch-two-sections" => Tyt() with { Kind = MockExamKind.Branch },
            "tyt-own-count" => Tyt([new("TYT-TUR", 30, 1, 1)]),
            "too-many-answers" => Tyt([new("TYT-SOS.TAR", 5, 4, 2)]),
            "block-and-subject" => Tyt([new("TYT-SOS", 20, 1, 1), new("TYT-SOS.TAR", 5, 1, 1)]),
            "religion-and-alternative" => Tyt([new("TYT-SOS.DKAB", 5, 1, 1), new("TYT-SOS.FEL2", 5, 1, 1)]),
            "marks-exceed" => Tyt(marks: [new("TYT-SOS.TAR", History, 0, 2)]),
            "unknown-topic" => Tyt(marks: [new("TYT-SOS.TAR", Guid.NewGuid(), 1, 0)]),
            _ => Tyt(marks: [new("TYT-SOS.TAR", Geography, 1, 0)]),
        };

        MockExamTemplate.Check(data, Template, Topics).ShouldNotBeNull().Code.ShouldBe(code);
    }

    [Fact]
    public void Edit_time_is_cut_to_microseconds()
    {
        var time = new DateTimeOffset(2026, 10, 31, 9, 0, 0, TimeSpan.Zero).AddTicks(1234567);

        EditTime.Of(time, time.AddDays(1)).UtcTicks.ShouldBe(time.UtcTicks - 7);
    }
}
