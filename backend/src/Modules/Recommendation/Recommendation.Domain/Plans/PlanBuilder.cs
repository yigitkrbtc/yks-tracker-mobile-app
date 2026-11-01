namespace Nirengi.Modules.Recommendation.Domain.Plans;

/// <summary>
/// Builds a week's plan from the analysis (ADR-0017). Pure and deterministic: the same input and kept items give
/// the same plan. Kept items (the frozen plan minus dismissals) stay first; free slots are filled by candidates in
/// kind order, then score, then topic code, under the caps (items, per subject, one tagging and one gap item).
/// </summary>
public static class PlanBuilder
{
    public static WeeklyPlan Build(RecommendationInput input, RecommendationRuleSet rules, IReadOnlyList<PlanItem> keep)
    {
        var items = keep.Where(k => !input.Dismissed.Contains(k.Key)).Take(rules.MaxItems).ToList();
        var keys = items.Select(i => i.Key).ToHashSet(StringComparer.Ordinal);
        var topics = items.Where(i => i.TopicId.HasValue).Select(i => i.TopicId!.Value).ToHashSet();
        var perSubject = items.Where(i => i.SubjectCode is not null)
            .GroupBy(i => i.SubjectCode!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var perKind = items.GroupBy(i => i.Kind).ToDictionary(g => g.Key, g => g.Count());

        foreach (var candidate in Candidates(input, rules))
        {
            if (items.Count >= rules.MaxItems)
            {
                break;
            }

            if (input.Dismissed.Contains(candidate.Key) || keys.Contains(candidate.Key)
                || (candidate.TopicId is { } topic && topics.Contains(topic))
                || (candidate.SubjectCode is { } subject && perSubject.GetValueOrDefault(subject) >= rules.MaxPerSubject)
                || perKind.GetValueOrDefault(candidate.Kind) >= KindCap(candidate.Kind, rules))
            {
                continue;
            }

            items.Add(candidate);
            keys.Add(candidate.Key);
            if (candidate.TopicId is { } t)
            {
                topics.Add(t);
            }

            if (candidate.SubjectCode is { } s)
            {
                perSubject[s] = perSubject.GetValueOrDefault(s) + 1;
            }

            perKind[candidate.Kind] = perKind.GetValueOrDefault(candidate.Kind) + 1;
        }

        return new WeeklyPlan(Week.StartOf(input.Today), rules.Version, items);
    }

    private static int KindCap(PlanItemKind kind, RecommendationRuleSet rules) => kind switch
    {
        PlanItemKind.TagMockExam or PlanItemKind.PracticeGap => 1,
        PlanItemKind.FirstRecord => rules.StarterTopics,
        _ => rules.MaxItems,
    };

    /// <summary>Every candidate, best first. A topic may appear under several kinds; the first one wins.</summary>
    private static IEnumerable<PlanItem> Candidates(RecommendationInput input, RecommendationRuleSet rules)
    {
        var states = input.States.Where(s => input.Topics.ContainsKey(s.TopicId)).ToDictionary(s => s.TopicId);

        // 1. Tag a recent mock exam: its wrong answers then feed the topic cards (M11c).
        var from = input.Today.AddDays(-rules.TagWindowDays);
        foreach (var tag in input.PendingTagging.Where(p => p.TakenOn >= from))
        {
            yield return PlanTexts.Tag(tag);
        }

        // 2. Focus: the M6 priority list as computed by the analysis.
        foreach (var id in input.Priorities)
        {
            if (input.Topics.TryGetValue(id, out var topic) && states.TryGetValue(id, out var state))
            {
                yield return PlanTexts.Focus(topic, state, rules.FocusTarget(state.QuestionsToNextLevel), Declared(input, id));
            }
        }

        // 3. Decline: a falling trend the analysis is reasonably sure of.
        foreach (var (state, topic) in Ranked(
            states.Values.Where(s => s.Trend == TopicTrend.Declining && s.Confidence >= TopicConfidence.Reasonable),
            input,
            s => 1 - (s.ProbabilityUp ?? 0.5)))
        {
            yield return PlanTexts.Decline(topic, state, rules.DeclineTarget, Declared(input, topic.TopicId));
        }

        // 4. Collect data on heavy topics the analysis cannot judge yet.
        foreach (var id in input.LowDataTopics)
        {
            if (input.Topics.TryGetValue(id, out var topic))
            {
                yield return PlanTexts.Collect(topic, states.GetValueOrDefault(id)?.Questions ?? 0, rules.CollectTarget, Declared(input, id));
            }
        }

        // 5. Review a strong topic left alone for weeks (FSRS replaces this in v1.1).
        var reviewBefore = input.Today.AddDays(-rules.ReviewAfterDays);
        foreach (var (state, topic) in Ranked(
            states.Values.Where(s => s.Mastery >= rules.ReviewMastery && s.LastStudiedOn is { } last && last <= reviewBefore),
            input,
            s => (input.Today.DayNumber - s.LastStudiedOn!.Value.DayNumber) * input.Topics[s.TopicId].ExpectedQuestions))
        {
            yield return PlanTexts.Review(topic, state, input.Today.DayNumber - state.LastStudiedOn!.Value.DayNumber, rules.ReviewTarget);
        }

        // 6. A mock section where practice accuracy runs well above mock accuracy (M11b).
        foreach (var gap in input.Gaps.OrderByDescending(g => g.Gap).ThenBy(g => g.Code, StringComparer.Ordinal))
        {
            yield return PlanTexts.Gap(gap);
        }

        // 7. No data at all: the heaviest topics of the student's tests, to get started.
        if (states.Count == 0)
        {
            foreach (var topic in input.Topics.Values
                .OrderByDescending(t => t.ExpectedQuestions)
                .ThenBy(t => t.Code, StringComparer.Ordinal))
            {
                yield return PlanTexts.First(topic, rules.CollectTarget, Declared(input, topic.TopicId));
            }
        }
    }

    private static IEnumerable<(TopicState State, TopicInfo Topic)> Ranked(
        IEnumerable<TopicState> states,
        RecommendationInput input,
        Func<TopicState, double> score) =>
        states.Select(s => (State: s, Topic: input.Topics[s.TopicId]))
            .OrderByDescending(x => score(x.State))
            .ThenBy(x => x.Topic.Code, StringComparer.Ordinal);

    private static DeclaredStatus Declared(RecommendationInput input, Guid topicId) =>
        input.Declared.GetValueOrDefault(topicId, DeclaredStatus.Unknown);
}
