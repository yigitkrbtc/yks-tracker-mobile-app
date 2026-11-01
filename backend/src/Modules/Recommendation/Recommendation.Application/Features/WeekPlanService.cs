using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Analytics.Contracts;
using Nirengi.Modules.Curriculum.Contracts;
using Nirengi.Modules.Recommendation.Application.Abstractions;
using Nirengi.Modules.Recommendation.Domain.Plans;
using Nirengi.Modules.Tracking.Contracts;

namespace Nirengi.Modules.Recommendation.Application.Features;

/// <param name="Done">Questions recorded on the item's topic this week.</param>
public sealed record PlanItemResponse(
    string Key,
    PlanItemKind Kind,
    Guid? TopicId,
    string? SectionCode,
    Guid? MockExamId,
    int? TargetQuestions,
    string Title,
    string Text,
    int Done,
    bool Completed);

/// <summary>This week's plan with live progress. <see cref="ETag"/> changes whenever anything shown changes.</summary>
public sealed record WeekPlanResponse(
    string ETag,
    DateOnly WeekStart,
    DateTimeOffset GeneratedAt,
    string RuleSetVersion,
    IReadOnlyList<PlanItemResponse> Items);

/// <summary>
/// Builds the week's plan on first read and keeps it frozen (ADR-0017). A thin plan (≤ 2 items) is filled again
/// when the analysis moves; a rule set change rebuilds it. Progress is computed on every read, never stored.
/// </summary>
internal sealed class WeekPlanService(
    IRecommendationStore store,
    IAnalysisDigestReader analysis,
    ICurriculumCatalog curriculum,
    ITrackingActivityReader tracking,
    IClock clock)
{
    private const int ThinPlan = 2;

    // Türkiye is UTC+3 all year (no DST since 2016).
    private static readonly TimeSpan TurkeyOffset = TimeSpan.FromHours(3);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<Result<WeekPlanResponse>> Get(Guid userId, CancellationToken cancellationToken) =>
        await Load(userId, null, cancellationToken);

    public async Task<Result<WeekPlanResponse>> Dismiss(Guid userId, string itemKey, DismissReason reason, CancellationToken cancellationToken) =>
        await Load(userId, (itemKey, reason), cancellationToken);

    private async Task<Result<WeekPlanResponse>> Load(Guid userId, (string Key, DismissReason Reason)? dismiss, CancellationToken cancellationToken)
    {
        var rules = RecommendationRuleSet.V1;
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.ToOffset(TurkeyOffset).DateTime);
        var weekStart = Week.StartOf(today);

        var digest = await analysis.Get(userId, cancellationToken);
        var catalog = await curriculum.GetAnalysisCatalog(cancellationToken);
        if (digest is null || catalog is null)
        {
            return RecommendationErrors.CurriculumNotLoaded;
        }

        var stored = await store.GetPlan(userId, weekStart, cancellationToken);
        var keep = stored is not null && stored.RuleSetVersion == rules.Version
            ? JsonSerializer.Deserialize<List<PlanItem>>(stored.Items, Json)!
            : [];
        var dismissed = (await store.GetFeedback(userId, weekStart, cancellationToken))
            .Select(f => f.ItemKey)
            .ToHashSet(StringComparer.Ordinal);

        ItemFeedback? feedback = null;
        if (dismiss is { } d)
        {
            if (stored is null || !keep.Any(i => i.Key == d.Key))
            {
                return RecommendationErrors.ItemNotFound;
            }

            dismissed.Add(d.Key);
            feedback = new ItemFeedback { UserId = userId, WeekStart = weekStart, ItemKey = d.Key, Reason = d.Reason, At = now };
        }

        var pending = await tracking.GetPendingTagging(userId, today.AddDays(-rules.TagWindowDays), cancellationToken);
        var rebuild = stored is null
            || stored.RuleSetVersion != rules.Version
            || feedback is not null
            || (keep.Count <= ThinPlan && stored.AnalysisStamp != digest.Stamp);
        if (rebuild)
        {
            var input = await Input(userId, today, digest, catalog, pending, dismissed, cancellationToken);
            var plan = PlanBuilder.Build(input, rules, keep);
            stored = new StoredPlan
            {
                UserId = userId,
                WeekStart = weekStart,
                RuleSetVersion = rules.Version,
                AnalysisStamp = digest.Stamp,
                Items = JsonSerializer.Serialize(plan.Items, Json),
                GeneratedAt = stored?.GeneratedAt ?? now,
            };
            keep = [.. plan.Items];
            await store.SavePlan(stored, feedback, cancellationToken);
        }

        var week = (await tracking.GetTopicActivity(userId, weekStart, today, cancellationToken))
            .GroupBy(a => a.TopicId)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Questions));
        var open = pending.Select(p => PlanProgress.TagKey(p.MockExamId, p.SectionCode)).ToHashSet(StringComparer.Ordinal);
        var items = keep.Select(i =>
        {
            var progress = PlanProgress.Of(i, week, open);
            return new PlanItemResponse(i.Key, i.Kind, i.TopicId, i.SectionCode, i.MockExamId, i.TargetQuestions, i.Title, i.Text, progress.Done, progress.Completed);
        }).ToList();

        return new WeekPlanResponse(ETag(stored!, items), weekStart, stored!.GeneratedAt, stored.RuleSetVersion, items);
    }

    private async Task<RecommendationInput> Input(
        Guid userId,
        DateOnly today,
        AnalysisDigest digest,
        AnalysisCatalogData catalog,
        IReadOnlyList<PendingTagging> pending,
        IReadOnlySet<string> dismissed,
        CancellationToken cancellationToken)
    {
        var activeTests = digest.ActiveTests.ToHashSet(StringComparer.Ordinal);
        var activeSubjects = catalog.Subjects
            .Where(s => s.TestCodes.Any(activeTests.Contains))
            .Select(s => s.Code)
            .ToHashSet(StringComparer.Ordinal);
        var topics = catalog.Topics
            .Where(t => t.IsActive && activeSubjects.Contains(t.SubjectCode))
            .ToDictionary(t => t.Id, t => new TopicInfo(t.Id, t.Code, t.Name, t.SubjectCode, (double)t.ExpectedQuestions));
        var sectionNames = catalog.MockSections.ToDictionary(s => s.Code, s => s.Name, StringComparer.Ordinal);
        var declared = (await tracking.GetProgress(userId, cancellationToken))
            .Where(p => Enum.TryParse<DeclaredStatus>(p.Status, out _))
            .ToDictionary(p => p.TopicId, p => Enum.Parse<DeclaredStatus>(p.Status));

        return new RecommendationInput(
            today,
            topics,
            [.. digest.Priorities.Select(p => p.TopicId)],
            digest.LowDataTopics,
            [.. digest.Topics.Select(t => new TopicState(
                t.TopicId,
                t.Mastery,
                t.MasteryLow,
                t.MasteryHigh,
                (TopicConfidence)(int)t.Confidence,
                (TopicTrend)(int)t.Trend,
                t.ProbabilityUp,
                t.QuestionsToNextLevel,
                t.Questions,
                t.LastStudiedOn))],
            [.. digest.Sections
                .Where(s => s.GapWarning && s.PracticeGap.HasValue)
                .Select(s => new SectionGap(s.Code, sectionNames.GetValueOrDefault(s.Code, s.Code), s.PracticeGap!.Value))],
            [.. pending.Select(p => new PendingTag(p.MockExamId, p.TakenOn, p.SectionCode, sectionNames.GetValueOrDefault(p.SectionCode, p.SectionCode), p.WrongAndBlank))],
            declared,
            dismissed);
    }

    private static string ETag(StoredPlan plan, IReadOnlyList<PlanItemResponse> items)
    {
        var content = $"{plan.WeekStart:O}|{plan.RuleSetVersion}|{JsonSerializer.Serialize(items, Json)}";
        return $"\"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)))[..32]}\"";
    }
}
