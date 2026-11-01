using System.Text.Json;
using Nirengi.Modules.Analytics.Application.Abstractions;
using Nirengi.Modules.Analytics.Application.Snapshots;
using Nirengi.Modules.Analytics.Contracts;
using Nirengi.Modules.Analytics.Engine;
using Nirengi.Modules.Curriculum.Contracts;

namespace Nirengi.Modules.Analytics.Application.Features;

internal sealed class AnalysisDigestReader(IAnalysisRefresher refresher, IAnalyticsStore store, ICurriculumCatalog curriculum)
    : IAnalysisDigestReader
{
    public async Task<AnalysisDigest?> Get(Guid userId, CancellationToken cancellationToken)
    {
        var analysis = await refresher.EnsureFresh(userId, cancellationToken);
        if (analysis is null)
        {
            return null;
        }

        var catalog = await curriculum.GetAnalysisCatalog(cancellationToken);
        var weights = catalog?.Topics.ToDictionary(t => t.Id, t => (double)t.ExpectedQuestions) ?? [];
        var summary = JsonSerializer.Deserialize<AnalysisSummary>(analysis.Summary, AnalysisRefresher.Json)!;
        var topics = await store.GetTopics(userId, cancellationToken);
        var sections = await store.GetSections(userId, cancellationToken);

        return new AnalysisDigest(
            GetAnalysisOverviewHandler.ETag(analysis),
            analysis.ComputedOn,
            analysis.RuleSetVersion,
            [.. summary.Tests.Where(t => t.IsActive).Select(t => t.Code)],
            [.. summary.Priorities.Select(p => new DigestPriority(p.TopicId, p.Priority, p.Mastery, p.ExpectedQuestions))],
            summary.LowDataTopics,
            [.. topics.Where(t => t.ConfidenceLevel != ConfidenceLevel.None).Select(t =>
            {
                var d = JsonSerializer.Deserialize<TopicDetails>(t.Details, AnalysisRefresher.Json)!;
                return new DigestTopic(
                    t.TopicId,
                    t.Mastery,
                    t.MasteryLow,
                    t.MasteryHigh,
                    (DigestConfidence)(int)t.ConfidenceLevel,
                    (DigestTrend)(int)t.Trend,
                    d.Trend.ProbabilityUp,
                    d.QuestionsToNextLevel,
                    d.Totals.Questions,
                    d.Recent.Count == 0 ? null : d.Recent.Max(r => r.Date),
                    weights.GetValueOrDefault(t.TopicId));
            })],
            [.. sections.OrderBy(s => s.SectionCode, StringComparer.Ordinal)
                .Select(s => new DigestSection(s.SectionCode, s.IsSufficient, s.PracticeGap, s.GapWarning))]);
    }
}
