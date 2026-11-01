using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Nirengi.Modules.Analytics.Engine;

using Shouldly;

namespace Nirengi.Analytics.GoldenTests;

/// <summary>
/// Runs the engine on every scenario in Scenarios/ and compares it with the values the independent reference
/// (reference/reference.py, scipy) computed. An engine change merges only when all scenarios pass (ADR-0003).
/// </summary>
public sealed class GoldenDatasetTests
{
    private const double Tolerance = 1e-7;

    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Scenarios");

    public static TheoryData<string> Scenarios()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Folder, "*.json").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    [Fact]
    public void Dataset_has_at_least_32_scenarios() => Scenarios().Count.ShouldBeGreaterThanOrEqualTo(32);

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Engine_matches_reference(string scenario)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, scenario + ".json")));
        var input = doc.RootElement.GetProperty("input");
        var expected = doc.RootElement.GetProperty("expected");
        expected.ValueKind.ShouldBe(JsonValueKind.Object, "Run reference/reference.py to fill the expected values.");

        var result = Analyzer.Analyze(ToInput(input), RuleSet.V1);

        result.RuleSetVersion.ShouldBe(expected.GetProperty("ruleSetVersion").GetString());
        result.InvalidRecords.ShouldBe(expected.GetProperty("invalidRecords").GetInt32());
        result.Priorities.ShouldBe(Ids(expected.GetProperty("priorities")), "priorities");
        result.LowDataTopics.ShouldBe(Ids(expected.GetProperty("lowDataTopics")), "lowDataTopics");

        foreach (var topic in expected.GetProperty("topics").EnumerateObject())
        {
            CompareTopic(topic.Name, result.Topics[Id(topic.Name)], topic.Value);
        }

        var subjects = expected.GetProperty("subjects");
        result.Subjects.Count.ShouldBe(subjects.EnumerateObject().Count());
        foreach (var subject in subjects.EnumerateObject())
        {
            CompareAggregate($"subject {subject.Name}", result.Subjects[subject.Name], subject.Value);
        }

        var tests = expected.GetProperty("tests");
        result.Tests.Count.ShouldBe(tests.EnumerateObject().Count());
        foreach (var test in tests.EnumerateObject())
        {
            CompareAggregate($"test {test.Name}", result.Tests[test.Name], test.Value);
        }

        var sections = expected.GetProperty("sections");
        result.Sections.Keys.Order(StringComparer.Ordinal).ShouldBe(
            sections.EnumerateObject().Select(s => s.Name).Order(StringComparer.Ordinal));
        foreach (var section in sections.EnumerateObject())
        {
            CompareSection(section.Name, result.Sections[section.Name], section.Value);
        }
    }

    private static void CompareTopic(string name, TopicAnalysis actual, JsonElement e)
    {
        var level = e.GetProperty("sufficiency").GetString();
        if (level == "none")
        {
            actual.HasData.ShouldBeFalse(name);
            actual.ConfidenceLevel.ShouldBe(ConfidenceLevel.None, name);
            actual.Band.ShouldBe(MasteryBand.None, name);
            actual.Trend.Label.ShouldBe(TrendLabel.None, name);
            actual.Priority.ShouldBeNull(name);
            CompareSignal(name, actual.MockSignal, e);
            return;
        }

        actual.ConfidenceLevel.ShouldBe(Enum.Parse<ConfidenceLevel>(level!, ignoreCase: true), name);
        actual.Band.ShouldBe(Enum.Parse<MasteryBand>(e.GetProperty("band").GetString()!, ignoreCase: true), name);
        actual.Trend.Label.ShouldBe(Enum.Parse<TrendLabel>(e.GetProperty("trend").GetString()!, ignoreCase: true), name);
        Near(actual.Mastery, e, "mastery", name);
        Near(actual.MasteryLow, e, "masteryLow", name);
        Near(actual.MasteryHigh, e, "masteryHigh", name);
        Near(actual.MarkingRate, e, "markingRate", name);
        actual.Optimistic.ShouldBe(e.GetProperty("optimistic").GetBoolean(), name);
        Near(actual.ExpectedNetPerQuestion, e, "expectedNetPerQuestion", name);
        Near(actual.EffectiveQuestions, e, "effectiveQuestions", name);
        Near(actual.Dispersion, e, "dispersion", name);
        Near(actual.Confidence, e, "confidence", name);
        Near(actual.Trend.ProbabilityUp, e, "trendProbabilityUp", name);
        Near(actual.Trend.Difference, e, "trendDifference", name);
        Near(actual.Priority, e, "priority", name);
        actual.Totals.ShouldBe(
            new TopicTotals(
                e.GetProperty("records").GetInt32(),
                e.GetProperty("questions").GetInt32(),
                e.GetProperty("correct").GetInt32(),
                e.GetProperty("wrong").GetInt32(),
                e.GetProperty("blank").GetInt32()),
            name);
        CompareSignal(name, actual.MockSignal, e);
    }

    private static void CompareSignal(string name, MockSignal? actual, JsonElement topic)
    {
        if (!topic.TryGetProperty("mockSignal", out var e))
        {
            actual.ShouldBeNull($"{name} mockSignal");
            return;
        }

        actual.ShouldNotBeNull($"{name} mockSignal");
        Near(actual.Exposure, e, "exposure", name);
        Near(actual.Rate, e, "rate", name);
        Near(actual.SubjectRate, e, "subjectRate", name);
        actual.Missed.ShouldBe(e.GetProperty("missed").GetInt32(), name);
        actual.Wrong.ShouldBe(e.GetProperty("wrong").GetInt32(), name);
        actual.Blank.ShouldBe(e.GetProperty("blank").GetInt32(), name);
        actual.Exams.ShouldBe(e.GetProperty("exams").GetInt32(), name);
        actual.Shown.ShouldBe(e.GetProperty("shown").GetBoolean(), name);
    }

    private static void CompareAggregate(string name, AggregateAnalysis actual, JsonElement e)
    {
        Near(actual.Mastery, e, "mastery", name);
        Near(actual.MasteryLow, e, "masteryLow", name);
        Near(actual.MasteryHigh, e, "masteryHigh", name);
        Near(actual.Coverage, e, "coverage", name);
        actual.StudiedTopics.ShouldBe(e.GetProperty("studiedTopics").GetInt32(), name);
        actual.TopicCount.ShouldBe(e.GetProperty("topics").GetInt32(), name);
    }

    private static void CompareSection(string name, SectionAnalysis actual, JsonElement e)
    {
        actual.Exams.ShouldBe(e.GetProperty("exams").GetInt32(), name);
        actual.IsSufficient.ShouldBe(e.GetProperty("sufficiency").GetString() == "sufficient", name);
        Near(actual.Accuracy, e, "accuracy", name);
        Near(actual.AccuracyLow, e, "accuracyLow", name);
        Near(actual.AccuracyHigh, e, "accuracyHigh", name);
        Near(actual.MarkingRate, e, "markingRate", name);
        Near(actual.ExpectedNet, e, "expectedNet", name);
        Near(actual.ExpectedNetLow, e, "expectedNetLow", name);
        Near(actual.ExpectedNetHigh, e, "expectedNetHigh", name);
        actual.Trend.Label.ShouldBe(Enum.Parse<TrendLabel>(e.GetProperty("trend").GetString()!, ignoreCase: true), name);
        Near(actual.Trend.ProbabilityUp, e, "trendProbabilityUp", name);
        Near(actual.PracticeGap, e, "practiceGap", name);
        actual.GapWarning.ShouldBe(e.GetProperty("gapWarning").GetBoolean(), name);
    }

    private static void Near(double? actual, JsonElement parent, string property, string context)
    {
        var label = $"{context}.{property}";
        if (!parent.TryGetProperty(property, out var e) || e.ValueKind == JsonValueKind.Null)
        {
            actual.ShouldBeNull(label);
            return;
        }

        actual.ShouldNotBeNull(label);
        var expected = e.GetDouble();
        Math.Abs(actual.Value - expected).ShouldBeLessThanOrEqualTo(Tolerance * Math.Max(1, Math.Abs(expected)), label);
    }

    private static AnalysisInput ToInput(JsonElement input)
    {
        var catalog = input.GetProperty("catalog");
        return new AnalysisInput(
            DateOnly.Parse(input.GetProperty("today").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
            new AnalysisCatalog(
                [.. catalog.GetProperty("topics").EnumerateArray().Select(t => new CatalogTopicInfo(
                    Id(t.GetProperty("id").GetString()!), t.GetProperty("subject").GetString()!, t.GetProperty("q").GetDouble()))],
                [.. catalog.GetProperty("subjects").EnumerateArray().Select(s => new CatalogSubjectInfo(
                    s.GetProperty("code").GetString()!, Strings(s.GetProperty("tests"))))],
                [.. catalog.GetProperty("sections").EnumerateArray().Select(s => new CatalogSectionInfo(
                    s.GetProperty("code").GetString()!,
                    s.GetProperty("test").GetString()!,
                    s.GetProperty("parent").GetString(),
                    Strings(s.GetProperty("subjects")),
                    s.GetProperty("questionCount").GetInt32()))]),
            Strings(input.GetProperty("activeTests")),
            [.. input.GetProperty("topicRecords").EnumerateArray().Select(r => new TopicRecord(
                Id(r.GetProperty("test").GetString()!),
                Id(r.GetProperty("topic").GetString()!),
                Date(r),
                r.GetProperty("total").GetInt32(),
                r.GetProperty("correct").GetInt32(),
                r.GetProperty("wrong").GetInt32(),
                r.GetProperty("blank").GetInt32(),
                r.GetProperty("repeat").GetBoolean()))],
            [.. input.GetProperty("mockExams").EnumerateArray().Select(m => new MockExamRecord(
                Id(m.GetProperty("id").GetString()!),
                Date(m),
                [.. m.GetProperty("sections").EnumerateArray().Select(s => new MockSectionRecord(
                    s.GetProperty("code").GetString()!,
                    s.GetProperty("questionCount").GetInt32(),
                    s.GetProperty("correct").GetInt32(),
                    s.GetProperty("wrong").GetInt32(),
                    s.GetProperty("full").GetBoolean(),
                    [.. s.GetProperty("marks").EnumerateArray().Select(k => new TopicMark(
                        Id(k.GetProperty("topic").GetString()!), k.GetProperty("wrong").GetInt32(), k.GetProperty("blank").GetInt32()))]))]))]);
    }

    private static DateOnly Date(JsonElement e) =>
        DateOnly.Parse(e.GetProperty("date").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(x => x.GetString()!)];

    private static Guid[] Ids(JsonElement array) => [.. array.EnumerateArray().Select(x => Id(x.GetString()!))];

    /// <summary>Scenario files name topics, tests and exams; the engine works with ids.</summary>
#pragma warning disable CA5351 // MD5 only maps readable names to stable test ids.
    private static Guid Id(string name) => new(MD5.HashData(Encoding.UTF8.GetBytes(name)));
#pragma warning restore CA5351
}
