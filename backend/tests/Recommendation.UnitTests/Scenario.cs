using System.Security.Cryptography;
using System.Text;
using Nirengi.Modules.Recommendation.Domain.Plans;

namespace Nirengi.Recommendation.UnitTests;

/// <summary>Readable scenario input: topics by code, ids derived from the code.</summary>
internal sealed class Scenario
{
    public static readonly DateOnly Today = new(2026, 11, 4); // Wednesday

    private readonly Dictionary<Guid, TopicInfo> _topics = [];
    private readonly List<Guid> _priorities = [];
    private readonly List<Guid> _lowData = [];
    private readonly List<TopicState> _states = [];
    private readonly List<SectionGap> _gaps = [];
    private readonly List<PendingTag> _pending = [];
    private readonly Dictionary<Guid, DeclaredStatus> _declared = [];
    private readonly HashSet<string> _dismissed = new(StringComparer.Ordinal);

    public static Guid Id(string code) => new(SHA256.HashData(Encoding.UTF8.GetBytes(code))[..16]);

    public Scenario Topic(string code, string subject, double q)
    {
        _topics[Id(code)] = new TopicInfo(Id(code), code, $"Konu {code}", subject, q);
        return this;
    }

    public Scenario State(
        string code,
        double mastery = 0.5,
        TopicConfidence confidence = TopicConfidence.Preliminary,
        TopicTrend trend = TopicTrend.Stable,
        double? probabilityUp = null,
        int? toNext = 30,
        int questions = 40,
        int daysAgo = 3)
    {
        _states.Add(new TopicState(
            Id(code), mastery, mastery - 0.05, mastery + 0.05, confidence, trend, probabilityUp, toNext, questions, Today.AddDays(-daysAgo)));
        return this;
    }

    public Scenario Priority(params string[] codes)
    {
        _priorities.AddRange(codes.Select(Id));
        return this;
    }

    public Scenario LowData(params string[] codes)
    {
        _lowData.AddRange(codes.Select(Id));
        return this;
    }

    public Scenario Gap(string code, double gap)
    {
        _gaps.Add(new SectionGap(code, $"Bölüm {code}", gap));
        return this;
    }

    public Scenario Pending(string exam, int daysAgo, string section, int missed = 5)
    {
        _pending.Add(new PendingTag(Id(exam), Today.AddDays(-daysAgo), section, $"Bölüm {section}", missed));
        return this;
    }

    public Scenario Declared(string code, DeclaredStatus status)
    {
        _declared[Id(code)] = status;
        return this;
    }

    public Scenario Dismiss(string key)
    {
        _dismissed.Add(key);
        return this;
    }

    public RecommendationInput Input() => new(Today, _topics, _priorities, _lowData, _states, _gaps, _pending, _declared, _dismissed);

    public WeeklyPlan Build(params PlanItem[] keep) => PlanBuilder.Build(Input(), RecommendationRuleSet.V1, keep);

    /// <summary>"kind:code[:target]" per item; section items use the section code.</summary>
    public static IReadOnlyList<string> Describe(WeeklyPlan plan, Scenario s) =>
        [.. plan.Items.Select(i =>
        {
            var target = i.TopicId is { } id ? s._topics[id].Code : i.SectionCode;
            return i.TargetQuestions is { } t ? $"{i.Kind}:{target}:{t}" : $"{i.Kind}:{target}";
        })];
}
