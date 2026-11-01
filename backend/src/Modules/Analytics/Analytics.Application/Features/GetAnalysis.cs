using System.Globalization;
using System.Text.Json;
using Nirengi.BuildingBlocks.Application.Messaging;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Analytics.Application.Abstractions;
using Nirengi.Modules.Analytics.Application.Snapshots;
using Nirengi.Modules.Analytics.Engine;
using Nirengi.Modules.Curriculum.Contracts;

namespace Nirengi.Modules.Analytics.Application.Features;

/// <summary>Compact status of a topic with data, for list badges.</summary>
public sealed record TopicStatusDto(
    Guid TopicId,
    double? Mastery,
    double? MasteryLow,
    double? MasteryHigh,
    ConfidenceLevel ConfidenceLevel,
    MasteryBand Band,
    TrendLabel Trend);

/// <summary>The analysis panel (S5). <see cref="ETag"/> changes whenever any number in it may change.</summary>
public sealed record AnalysisOverviewResponse(
    string ETag,
    DateTimeOffset ComputedAt,
    DateOnly ComputedOn,
    string RuleSetVersion,
    int InvalidRecords,
    IReadOnlyList<AggregateDto> Tests,
    IReadOnlyList<AggregateDto> Subjects,
    IReadOnlyList<PriorityDto> Priorities,
    IReadOnlyList<Guid> LowDataTopics,
    IReadOnlyList<SectionDto> Sections,
    IReadOnlyList<TopicStatusDto> Topics);

/// <summary>The topic card. A topic without any record comes back with <c>ConfidenceLevel = None</c>, not 404.</summary>
public sealed record TopicAnalysisResponse(
    Guid TopicId,
    DateTimeOffset ComputedAt,
    string RuleSetVersion,
    TopicDetails Analysis);

public sealed record GetAnalysisOverviewQuery(Guid UserId) : IQuery<AnalysisOverviewResponse>;

public sealed record GetTopicAnalysisQuery(Guid UserId, Guid TopicId) : IQuery<TopicAnalysisResponse>;

internal sealed class GetAnalysisOverviewHandler(IAnalysisRefresher refresher, IAnalyticsStore store)
    : IQueryHandler<GetAnalysisOverviewQuery, AnalysisOverviewResponse>
{
    public async Task<Result<AnalysisOverviewResponse>> Handle(GetAnalysisOverviewQuery query, CancellationToken cancellationToken)
    {
        var analysis = await refresher.EnsureFresh(query.UserId, cancellationToken);
        if (analysis is null)
        {
            return AnalyticsErrors.CurriculumNotLoaded;
        }

        var summary = JsonSerializer.Deserialize<AnalysisSummary>(analysis.Summary, AnalysisRefresher.Json)!;
        var topics = await store.GetTopics(query.UserId, cancellationToken);
        var sections = await store.GetSections(query.UserId, cancellationToken);

        return new AnalysisOverviewResponse(
            ETag(analysis),
            analysis.ComputedAt,
            analysis.ComputedOn,
            analysis.RuleSetVersion,
            analysis.InvalidRecords,
            summary.Tests,
            summary.Subjects,
            summary.Priorities,
            summary.LowDataTopics,
            [.. sections.OrderBy(s => s.SectionCode, StringComparer.Ordinal)
                .Select(s => JsonSerializer.Deserialize<SectionDto>(s.Details, AnalysisRefresher.Json)!)],
            [.. topics.Where(t => t.ConfidenceLevel != ConfidenceLevel.None)
                .Select(t => new TopicStatusDto(t.TopicId, t.Mastery, t.MasteryLow, t.MasteryHigh, t.ConfidenceLevel, t.Band, t.Trend))]);
    }

    /// <summary>Strong validator: same stamp, day and rule set give byte-identical content.</summary>
    internal static string ETag(UserAnalysis analysis) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"\"{Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{analysis.SourceStamp}|{analysis.ComputedOn:O}|{analysis.RuleSetVersion}")))[..32]}\"");
}

internal sealed class GetTopicAnalysisHandler(IAnalysisRefresher refresher, IAnalyticsStore store, ICurriculumCatalog curriculum)
    : IQueryHandler<GetTopicAnalysisQuery, TopicAnalysisResponse>
{
    private static readonly TopicDetails Empty = new(
        null, null, null, null, false, null, null, null, ConfidenceLevel.None,
        AnalysisRefresher.QuestionsToNextLevel(TopicAnalysis.Empty, RuleSet.V1),
        MasteryBand.None, new TrendDto(TrendLabel.None, null, null), null, new TotalsDto(0, 0, 0, 0, 0), null, []);

    public async Task<Result<TopicAnalysisResponse>> Handle(GetTopicAnalysisQuery query, CancellationToken cancellationToken)
    {
        if ((await curriculum.GetTopics([query.TopicId], cancellationToken)).Count == 0)
        {
            return AnalyticsErrors.TopicNotFound;
        }

        var analysis = await refresher.EnsureFresh(query.UserId, cancellationToken);
        if (analysis is null)
        {
            return AnalyticsErrors.CurriculumNotLoaded;
        }

        var snapshot = await store.GetTopic(query.UserId, query.TopicId, cancellationToken);
        var details = snapshot is null ? Empty : JsonSerializer.Deserialize<TopicDetails>(snapshot.Details, AnalysisRefresher.Json)!;
        return new TopicAnalysisResponse(query.TopicId, analysis.ComputedAt, analysis.RuleSetVersion, details);
    }
}
