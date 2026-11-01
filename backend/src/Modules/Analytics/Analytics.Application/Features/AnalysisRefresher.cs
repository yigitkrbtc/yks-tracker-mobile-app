using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.Modules.Analytics.Application.Abstractions;
using Nirengi.Modules.Analytics.Application.Snapshots;
using Nirengi.Modules.Analytics.Engine;
using Nirengi.Modules.Curriculum.Contracts;
using Nirengi.Modules.Tracking.Contracts;

namespace Nirengi.Modules.Analytics.Application.Features;

/// <summary>Keeps a student's stored analysis current (ADR-0016: on change events and on read).</summary>
public interface IAnalysisRefresher
{
    /// <summary>
    /// Returns the stored analysis, recomputing it first when the records, profile, curriculum, rule set or the day
    /// changed since. Null when the curriculum is not loaded yet.
    /// </summary>
    Task<UserAnalysis?> EnsureFresh(Guid userId, CancellationToken cancellationToken);
}

internal sealed class AnalysisRefresher(
    IAnalyticsStore store,
    ITrackingAnalyticsReader tracking,
    ICurriculumCatalog curriculum,
    IClock clock) : IAnalysisRefresher
{
    private const int RecentRecords = 8;

    // Türkiye is UTC+3 all year (no DST since 2016).
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<UserAnalysis?> EnsureFresh(Guid userId, CancellationToken cancellationToken)
    {
        var rules = RuleSet.V1;
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.ToOffset(TurkeyOffset).DateTime);

        var version = await curriculum.GetVersion(cancellationToken);
        if (version is null)
        {
            return null;
        }

        var stamp = Stamp(await tracking.GetStamp(userId, cancellationToken), version);
        var current = await store.Get(userId, cancellationToken);
        if (current is not null && current.SourceStamp == stamp && current.ComputedOn == today && current.RuleSetVersion == rules.Version)
        {
            return current;
        }

        var catalog = await curriculum.GetAnalysisCatalog(cancellationToken);
        if (catalog is null)
        {
            return null;
        }

        var history = await tracking.GetHistory(userId, cancellationToken);
        var input = ToInput(today, catalog, history);
        var result = Analyzer.Analyze(input, rules);

        var analysis = new UserAnalysis
        {
            UserId = userId,
            RuleSetVersion = rules.Version,
            SourceStamp = stamp,
            ComputedOn = today,
            ComputedAt = now,
            InvalidRecords = result.InvalidRecords,
            Summary = JsonSerializer.Serialize(Summary(result, input, catalog), Json),
        };
        var topics = Topics(userId, result, history, rules);
        var sections = result.Sections.Select(s => Section(userId, s.Key, s.Value)).ToList();
        await store.Replace(analysis, topics, sections, cancellationToken);
        return analysis;
    }

    // The catalog version covers curriculum and weight changes; the profile time covers score type changes.
    private static string Stamp(TrackingStamp tracking, string curriculumVersion) =>
        string.Create(CultureInfo.InvariantCulture, $"{tracking.ChangeSeq}|{tracking.ProfileUpdatedAt?.UtcTicks ?? 0}|{curriculumVersion}");

    internal static AnalysisInput ToInput(DateOnly today, AnalysisCatalogData catalog, StudentHistory history)
    {
        var activeTests = catalog.ScoreTypes
            .Where(s => history.ScoreTypes.Contains(s.Code, StringComparer.Ordinal))
            .SelectMany(s => s.TestCodes)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (activeTests.Count == 0)
        {
            // Before onboarding every test counts.
            activeTests = [.. catalog.Subjects.SelectMany(s => s.TestCodes).Distinct(StringComparer.Ordinal)];
        }

        return new AnalysisInput(
            today,
            new AnalysisCatalog(
                [.. catalog.Topics.Select(t => new CatalogTopicInfo(t.Id, t.SubjectCode, (double)t.ExpectedQuestions))],
                [.. catalog.Subjects.Select(s => new CatalogSubjectInfo(s.Code, s.TestCodes))],
                [.. catalog.MockSections.Select(s => new CatalogSectionInfo(s.Code, s.TestCode, s.ParentCode, s.SubjectCodes, s.QuestionCount))]),
            activeTests,
            [.. history.TopicResults.Select(r => new TopicRecord(
                r.TopicTestId, r.TopicId, r.StudiedOn, r.Total, r.Correct, r.Wrong, r.Blank, r.IsRepeat))],
            [.. history.MockExams.Select(e => new MockExamRecord(
                e.Id,
                e.TakenOn,
                [.. e.Sections.Select(s => new MockSectionRecord(
                    s.SectionCode,
                    s.QuestionCount,
                    s.Correct,
                    s.Wrong,
                    s.IsFullyTagged,
                    [.. s.Marks.Select(m => new TopicMark(m.TopicId, m.Wrong, m.Blank))]))]))]);
    }

    private static AnalysisSummary Summary(AnalysisResult result, AnalysisInput input, AnalysisCatalogData catalog)
    {
        var activeTests = input.ActiveTests.ToHashSet(StringComparer.Ordinal);
        var activeSubjects = catalog.Subjects
            .Where(s => s.TestCodes.Any(activeTests.Contains))
            .Select(s => s.Code)
            .ToHashSet(StringComparer.Ordinal);
        var weights = catalog.Topics.ToDictionary(t => t.Id, t => (double)t.ExpectedQuestions);
        var rules = RuleSet.V1;

        return new AnalysisSummary(
            [.. result.Tests.Select(t => Aggregate(t.Key, activeTests.Contains(t.Key), t.Value))],
            [.. catalog.Subjects
                .Where(s => result.Subjects.ContainsKey(s.Code))
                .Select(s => Aggregate(s.Code, activeSubjects.Contains(s.Code), result.Subjects[s.Code]))],
            [.. result.Priorities.Select(id =>
            {
                var topic = result.Topics[id];
                var q = weights[id];
                var mastery = topic.Mastery!.Value;
                var gain = q * Math.Max(0, rules.TargetMastery - mastery) * RuleSet.Learnability(mastery);
                return new PriorityDto(id, topic.Priority!.Value, mastery, q, gain, topic.Priority.Value - gain);
            })],
            result.LowDataTopics);
    }

    private static AggregateDto Aggregate(string code, bool active, AggregateAnalysis a) =>
        new(code, active, a.Mastery, a.MasteryLow, a.MasteryHigh, a.Coverage, a.StudiedTopics, a.TopicCount);

    private static List<TopicSnapshot> Topics(Guid userId, AnalysisResult result, StudentHistory history, RuleSet rules)
    {
        var recent = history.TopicResults
            .GroupBy(r => r.TopicId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.StudiedOn)
                    .Take(RecentRecords)
                    .Select(r => new RecentRecordDto(r.StudiedOn, r.Total, r.Correct, r.Wrong, r.Blank, r.IsRepeat))
                    .ToList());

        var rows = new List<TopicSnapshot>();
        foreach (var (id, t) in result.Topics)
        {
            if (!t.HasData && t.MockSignal is null)
            {
                continue;
            }

            var details = new TopicDetails(
                t.Mastery,
                t.MasteryLow,
                t.MasteryHigh,
                t.MarkingRate,
                t.Optimistic,
                t.ExpectedNetPerQuestion,
                t.EffectiveQuestions,
                t.Confidence,
                t.ConfidenceLevel,
                QuestionsToNextLevel(t, rules),
                t.Band,
                new TrendDto(t.Trend.Label, t.Trend.ProbabilityUp, t.Trend.Difference),
                t.Priority,
                new TotalsDto(t.Totals.Records, t.Totals.Questions, t.Totals.Correct, t.Totals.Wrong, t.Totals.Blank),
                t.MockSignal is { } s ? new MockSignalDto(s.Exposure, s.Missed, s.Wrong, s.Blank, s.Exams, s.Rate, s.SubjectRate, s.Shown) : null,
                recent.GetValueOrDefault(id) ?? []);

            rows.Add(new TopicSnapshot
            {
                UserId = userId,
                TopicId = id,
                Mastery = t.Mastery,
                MasteryLow = t.MasteryLow,
                MasteryHigh = t.MasteryHigh,
                Confidence = t.Confidence,
                ConfidenceLevel = t.ConfidenceLevel,
                Band = t.Band,
                Trend = t.Trend.Label,
                Priority = t.Priority,
                Details = JsonSerializer.Serialize(details, Json),
            });
        }

        return rows;
    }

    /// <summary>
    /// Fresh questions (today, same dispersion) that lift the confidence over the next level's threshold:
    /// n/φ / (n/φ + k) ≥ t  ⇔  n ≥ φ·t·k / (1 − t).
    /// </summary>
    internal static int? QuestionsToNextLevel(TopicAnalysis topic, RuleSet rules)
    {
        double? threshold = topic.ConfidenceLevel switch
        {
            ConfidenceLevel.None or ConfidenceLevel.VeryLow => rules.PreliminaryFrom,
            ConfidenceLevel.Preliminary => rules.ReasonableFrom,
            ConfidenceLevel.Reasonable => rules.ReliableFrom,
            _ => null,
        };
        if (threshold is not { } t)
        {
            return null;
        }

        var phi = topic.Dispersion ?? 1;
        var needed = (phi * t * rules.ConfidenceK / (1 - t)) - (topic.EffectiveQuestions ?? 0);
        return Math.Max(1, (int)Math.Ceiling(needed));
    }

    private static SectionSnapshot Section(Guid userId, string code, SectionAnalysis s)
    {
        var dto = new SectionDto(
            code,
            s.Exams,
            s.IsSufficient,
            s.Accuracy,
            s.AccuracyLow,
            s.AccuracyHigh,
            s.MarkingRate,
            s.ExpectedNet,
            s.ExpectedNetLow,
            s.ExpectedNetHigh,
            new TrendDto(s.Trend.Label, s.Trend.ProbabilityUp, s.Trend.Difference),
            s.PracticeGap,
            s.GapWarning);
        return new SectionSnapshot
        {
            UserId = userId,
            SectionCode = code,
            Exams = s.Exams,
            IsSufficient = s.IsSufficient,
            Accuracy = s.Accuracy,
            Trend = s.Trend.Label,
            PracticeGap = s.PracticeGap,
            GapWarning = s.GapWarning,
            Details = JsonSerializer.Serialize(dto, Json),
        };
    }
}
