using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Versions;
using Shouldly;
using static Nirengi.Curriculum.UnitTests.DraftApplierTests;

namespace Nirengi.Curriculum.UnitTests;

/// <summary>ADR-0019: seed imports and rollbacks are diffs; applying the diff reproduces the target.</summary>
public sealed class ContentDiffTests
{
    private static readonly HashSet<string> Subjects = new(StringComparer.Ordinal) { "TYT.MAT", "TYT.TUR" };

    private static readonly CurriculumContent Live = new(
        [Topic("TYT.MAT.01", "TYT.MAT", 0), Topic("TYT.MAT.02", "TYT.MAT", 1), Topic("TYT.TUR.01", "TYT.TUR", 0)],
        [new SourceState("A", "A", SourceKind.Publisher, 0, true), new SourceState("B", "B", SourceKind.Publisher, 1, true)],
        [Calendar(2027, null)]);

    [Fact]
    public void Same_content_has_no_operations() => ContentDiff.Between(Live, Live).ShouldBeEmpty();

    [Fact]
    public void Missing_topics_and_sources_are_retired_never_deleted()
    {
        var desired = Live with { Topics = [Live.Topics[0], Live.Topics[2]], Sources = [Live.Sources[0]] };

        ContentDiff.Between(Live, desired).ShouldBe(
            [new RetireTopic("TYT.MAT.02"), new RetireSource("B")],
            ignoreOrder: true);
    }

    [Fact]
    public void Applying_the_diff_reproduces_the_desired_content()
    {
        var desired = new CurriculumContent(
            [
                Live.Topics[0] with { Name = "Değişti", SortOrder = 5 },
                Live.Topics[1] with { IsActive = false },
                Live.Topics[2],
                Topic("TYT.TUR.02", "TYT.TUR", 1),
                Topic("TYT.TUR.03", "TYT.TUR", 2, active: false),
            ],
            [Live.Sources[0] with { Name = "A2" }, Live.Sources[1], new SourceState("C", "C", SourceKind.Official, 2, true)],
            [Calendar(2027, "yeni not"), Calendar(2028, null)]);

        var applied = DraftApplier.Apply(Live, Subjects, new HashSet<string>(StringComparer.Ordinal), ContentDiff.Between(Live, desired));

        applied.IsSuccess.ShouldBeTrue(applied.IsFailure ? applied.Error.Message : null);
        Normalize(applied.Value.Content).ShouldBe(Normalize(desired));
    }

    [Fact]
    public void Rollback_reactivates_a_retired_topic()
    {
        var live = Live with { Topics = [Live.Topics[0], Live.Topics[1] with { IsActive = false }, Live.Topics[2]] };

        ContentDiff.Between(live, Live).ShouldBe([new ReactivateTopic("TYT.MAT.02")]);
    }

    /// <summary>Records holding lists compare by reference; compare their JSON instead.</summary>
    private static string Normalize(CurriculumContent content) => CurriculumJson.Serialize(content with
    {
        Topics = [.. content.Topics.OrderBy(t => t.Code, StringComparer.Ordinal)],
        Sources = [.. content.Sources.OrderBy(s => s.Code, StringComparer.Ordinal)],
    });
}
