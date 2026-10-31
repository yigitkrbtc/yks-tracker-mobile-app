using Nirengi.Modules.Analytics.Engine;

using Shouldly;

namespace Nirengi.Analytics.GoldenTests;

/// <summary>Numerical checks against scipy 1.x values, plus properties the golden scenarios do not pin down.</summary>
public sealed class EngineTests
{
    [Theory]
    [InlineData(0.3, 2, 3, 0.34829999999999994)]
    [InlineData(0.95, 50.5, 3.2, 0.5553952639838169)]
    [InlineData(0.5, 3000, 2900, 0.0964561010983565)]
    [InlineData(0.01, 1, 1, 0.01)]
    [InlineData(0.7, 0.5, 0.5, 0.6309898804344546)]
    public void Cdf_matches_scipy(double x, double a, double b, double expected) =>
        BetaMath.Cdf(x, a, b).ShouldBe(expected, 1e-12);

    [Theory]
    [InlineData(0.05, 13, 7, 0.47003287877805244)]
    [InlineData(0.95, 13, 7, 0.8124956884945995)]
    [InlineData(0.999, 2, 200, 0.04500819669210767)]
    [InlineData(0.001, 1500.5, 700.25, 0.6506900075529948)]
    public void Quantile_matches_scipy(double p, double a, double b, double expected) =>
        BetaMath.Quantile(p, a, b).ShouldBe(expected, 1e-10);

    [Theory]
    [InlineData(15, 12, 61, 21, 0.03549921339174196)]
    [InlineData(1, 1, 1, 1, 0.5)]
    [InlineData(400, 100, 380, 120, 0.9370136436899147)]
    [InlineData(2.5, 1, 1, 3.5, 0.946310672414994)]
    public void Probability_greater_matches_scipy(double a1, double b1, double a2, double b2, double expected) =>
        BetaMath.ProbabilityGreater(a1, b1, a2, b2).ShouldBe(expected, 1e-9);

    [Fact]
    public void Record_order_does_not_change_the_result()
    {
        var input = RandomInput(seed: 7);
        var shuffled = input with { TopicRecords = [.. input.TopicRecords.Reverse()] };

        var a = Analyzer.Analyze(input, RuleSet.V1);
        var b = Analyzer.Analyze(shuffled, RuleSet.V1);

        foreach (var (id, topic) in a.Topics)
        {
            // Sums run in a different order, so allow rounding noise.
            (topic.Mastery ?? 0).ShouldBe(b.Topics[id].Mastery ?? 0, 1e-12);
            (topic.Confidence ?? 0).ShouldBe(b.Topics[id].Confidence ?? 0, 1e-12);
            topic.Trend.Label.ShouldBe(b.Topics[id].Trend.Label);
        }

        b.Priorities.ShouldBe(a.Priorities);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Intervals_hold_the_mean_and_stay_in_unit_range(int seed)
    {
        var result = Analyzer.Analyze(RandomInput(seed), RuleSet.V1);

        foreach (var topic in result.Topics.Values.Where(t => t.HasData))
        {
            topic.MasteryLow!.Value.ShouldBeInRange(0, topic.Mastery!.Value);
            topic.MasteryHigh!.Value.ShouldBeInRange(topic.Mastery.Value, 1);
            topic.Confidence!.Value.ShouldBeInRange(0, 1);
            topic.MarkingRate!.Value.ShouldBeInRange(0, 1);
        }

        foreach (var agg in result.Subjects.Values)
        {
            agg.Mastery.ShouldBeInRange(agg.MasteryLow, agg.MasteryHigh);
            agg.Coverage.ShouldBeInRange(0, 1);
        }
    }

    [Fact]
    public void More_fresh_questions_never_lower_confidence()
    {
        var topic = Guid.NewGuid();
        var catalog = new AnalysisCatalog([new CatalogTopicInfo(topic, "MAT", 4)], [new CatalogSubjectInfo("MAT", ["TYT-MAT"])], []);
        var today = new DateOnly(2026, 10, 31);
        var records = new List<TopicRecord>();
        var previous = 0.0;
        for (var i = 0; i < 20; i++)
        {
            records.Add(new TopicRecord(Guid.NewGuid(), topic, today, 10, 7, 2, 1, false));
            var confidence = Analyzer.Analyze(new AnalysisInput(today, catalog, ["TYT-MAT"], records, []), RuleSet.V1)
                .Topics[topic].Confidence!.Value;
            confidence.ShouldBeGreaterThanOrEqualTo(previous);
            previous = confidence;
        }
    }

    /// <summary>Read-time recompute budget (ADR-0016): 2,000 topic lines + 200 mocks take ≈ 20 ms; the bound leaves CI headroom.</summary>
    [Fact]
    public void Heavy_history_is_analysed_quickly()
    {
        var random = new Random(11);
        var subjects = Enumerable.Range(0, 20).Select(i => $"S{i}").ToArray();
        var topics = Enumerable.Range(0, 370)
            .Select(i => new CatalogTopicInfo(Guid.NewGuid(), subjects[i % subjects.Length], random.NextDouble() * 4))
            .ToList();
        var today = new DateOnly(2026, 10, 31);
        var records = Enumerable.Range(0, 2000).Select(_ =>
        {
            var c = random.Next(1, 15);
            var w = random.Next(6);
            return new TopicRecord(Guid.NewGuid(), topics[random.Next(topics.Count)].Id, today.AddDays(-random.Next(365)), c + w, c, w, 0, false);
        }).ToList();
        var sections = subjects.Select(s => new CatalogSectionInfo(s, s, null, [s], 20)).ToList();
        var exams = Enumerable.Range(0, 200).Select(i => new MockExamRecord(
            Guid.NewGuid(),
            today.AddDays(-random.Next(365)),
            [.. subjects.Take(4).Select(s => new MockSectionRecord(s, 20, 12, 4, true, [new TopicMark(topics[random.Next(topics.Count)].Id, 0, 0)]))])).ToList();
        var input = new AnalysisInput(
            today,
            new AnalysisCatalog(topics, [.. subjects.Select(s => new CatalogSubjectInfo(s, [s]))], sections),
            subjects,
            records,
            exams);

        Analyzer.Analyze(input, RuleSet.V1); // warm-up (JIT)
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Analyzer.Analyze(input, RuleSet.V1);
        watch.Stop();

        TestContext.Current.SendDiagnosticMessage($"Analyze: {watch.ElapsedMilliseconds} ms");
        watch.ElapsedMilliseconds.ShouldBeLessThan(500);
    }

    private static AnalysisInput RandomInput(int seed)
    {
        var random = new Random(seed);
        var subjects = new[] { "MAT", "TUR" };
        var topics = Enumerable.Range(0, 12)
            .Select(i => new CatalogTopicInfo(Guid.NewGuid(), subjects[i % 2], 1 + random.Next(10)))
            .ToList();
        var today = new DateOnly(2026, 10, 31);
        var records = Enumerable.Range(0, 80).Select(_ =>
        {
            var c = random.Next(15);
            var w = random.Next(10);
            var b = random.Next(5);
            return new TopicRecord(
                Guid.NewGuid(),
                topics[random.Next(topics.Count)].Id,
                today.AddDays(-random.Next(100)),
                Math.Max(1, c + w + b),
                c + w + b == 0 ? 1 : c,
                w,
                b,
                random.Next(5) == 0);
        }).ToList();
        return new AnalysisInput(
            today,
            new AnalysisCatalog(topics, [new CatalogSubjectInfo("MAT", ["TYT-MAT"]), new CatalogSubjectInfo("TUR", ["TYT-TUR"])], []),
            ["TYT-MAT", "TYT-TUR"],
            records,
            []);
    }
}
