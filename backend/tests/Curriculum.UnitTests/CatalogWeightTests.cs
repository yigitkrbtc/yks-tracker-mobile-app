using Nirengi.Modules.Curriculum.Contracts;
using Shouldly;

namespace Nirengi.Curriculum.UnitTests;

/// <summary>ADR-0019: a student's q_i is the sum over the tests of their score type.</summary>
public sealed class CatalogWeightTests
{
    private static readonly CatalogWeightedTopic History = new(
        Guid.NewGuid(),
        "AYT.TAR",
        new Dictionary<string, decimal>(StringComparer.Ordinal) { ["AYT-TDE-SB1"] = 0.8m, ["AYT-SB2"] = 1.1m },
        true,
        "AYT.TAR.10",
        "Tarih konusu");

    [Fact]
    public void Equal_weight_student_counts_only_the_first_social_test() =>
        History.ExpectedFor(new HashSet<string>(StringComparer.Ordinal) { "TYT-TUR", "AYT-TDE-SB1", "AYT-MAT" }).ShouldBe(0.8m);

    [Fact]
    public void Verbal_student_counts_both_social_tests() =>
        History.ExpectedFor(new HashSet<string>(StringComparer.Ordinal) { "AYT-TDE-SB1", "AYT-SB2" }).ShouldBe(1.9m);

    [Fact]
    public void Numerical_student_has_no_weight() =>
        History.ExpectedFor(new HashSet<string>(StringComparer.Ordinal) { "AYT-MAT", "AYT-FEN" }).ShouldBe(0m);

    [Fact]
    public void Merge_targets_follow_chains_and_ignore_splits()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid(), d = Guid.NewGuid();

        var targets = Catalog(new(a, b, true), new(b, c, true), new(d, a, false)).MergeTargets();

        targets[a].ShouldBe(c);
        targets[b].ShouldBe(c);
        targets.ContainsKey(d).ShouldBeFalse();
        targets.ContainsKey(c).ShouldBeFalse();
    }

    [Fact]
    public void Merge_targets_stop_on_a_cycle()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();

        var targets = Catalog(new(a, b, true), new(b, a, true)).MergeTargets();

        targets.Count.ShouldBe(2); // terminates; publishing never writes a cycle (reactivation drops links)
    }

    private static AnalysisCatalogData Catalog(params CatalogLineage[] lineage) => new("v", 2027, [], [], [], [], lineage);
}
