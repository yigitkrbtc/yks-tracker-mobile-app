using Nirengi.Modules.Recommendation.Domain.Plans;
using Shouldly;
using static Nirengi.Modules.Recommendation.Domain.Plans.PlanItemKind;

namespace Nirengi.Recommendation.UnitTests;

/// <summary>Rule scenarios of ADR-0017. Each names its input and the exact plan expected.</summary>
public sealed class PlanBuilderTests
{
    private static Scenario Math3() => new Scenario()
        .Topic("MAT.1", "TYT.TMAT", 4).Topic("MAT.2", "TYT.TMAT", 3).Topic("MAT.3", "TYT.TMAT", 2)
        .Topic("TUR.1", "TYT.TUR", 5).Topic("TUR.2", "TYT.TUR", 1)
        .Topic("FIZ.1", "TYT.FIZ", 1.5).Topic("KIM.1", "TYT.KIM", 1.2);

    private static void Expect(Scenario s, WeeklyPlan plan, params string[] expected) =>
        Scenario.Describe(plan, s).ShouldBe(expected);

    [Fact]
    public void S01_no_topics_no_items() =>
        new Scenario().Build().Items.ShouldBeEmpty();

    [Fact]
    public void S02_no_data_suggests_the_three_heaviest_topics()
    {
        var s = new Scenario().Topic("A.1", "A", 5).Topic("B.1", "B", 4).Topic("C.1", "C", 3).Topic("D.1", "D", 2);
        Expect(s, s.Build(), "FirstRecord:A.1:20", "FirstRecord:B.1:20", "FirstRecord:C.1:20");
    }

    [Fact]
    public void S03_starter_topics_respect_the_subject_cap()
    {
        var s = Math3();
        Expect(s, s.Build(), "FirstRecord:TUR.1:20", "FirstRecord:MAT.1:20", "FirstRecord:MAT.2:20");
    }

    [Fact]
    public void S04_focus_follows_the_priority_order()
    {
        var s = Math3().State("TUR.1").State("MAT.1").State("FIZ.1").Priority("FIZ.1", "TUR.1", "MAT.1");
        Expect(s, s.Build(), "Focus:FIZ.1:30", "Focus:TUR.1:30", "Focus:MAT.1:30");
    }

    [Theory]
    [InlineData(null, 20)]
    [InlineData(5, 20)]
    [InlineData(23, 30)]
    [InlineData(40, 40)]
    [InlineData(90, 60)]
    public void S05_focus_target_is_rounded_and_clamped(int? toNext, int expected)
    {
        var s = Math3().State("TUR.1", toNext: toNext).Priority("TUR.1");
        s.Build().Items.Single().TargetQuestions.ShouldBe(expected);
    }

    [Fact]
    public void S06_at_most_two_items_per_subject()
    {
        var s = Math3().State("MAT.1").State("MAT.2").State("MAT.3").State("TUR.1").Priority("MAT.1", "MAT.2", "MAT.3", "TUR.1");
        Expect(s, s.Build(), "Focus:MAT.1:30", "Focus:MAT.2:30", "Focus:TUR.1:30");
    }

    [Fact]
    public void S07_at_most_five_items()
    {
        var s = new Scenario();
        foreach (var c in "ABCDEFG")
        {
            s.Topic($"{c}.1", c.ToString(), 2).State($"{c}.1").Priority($"{c}.1");
        }

        s.Build().Items.Count.ShouldBe(5);
    }

    [Fact]
    public void S08_recent_untagged_mock_comes_first()
    {
        var s = Math3().State("TUR.1").Priority("TUR.1").Pending("E1", 2, "TYT-TUR");
        Expect(s, s.Build(), "TagMockExam:TYT-TUR", "Focus:TUR.1:30");
    }

    [Fact]
    public void S09_mock_older_than_two_weeks_is_not_suggested()
    {
        var s = Math3().State("TUR.1").Priority("TUR.1").Pending("E1", 15, "TYT-TUR");
        Expect(s, s.Build(), "Focus:TUR.1:30");
    }

    [Fact]
    public void S10_only_the_newest_tagging_item()
    {
        var s = Math3().Pending("E2", 1, "TYT-MAT").Pending("E1", 5, "TYT-TUR").State("TUR.1").Priority("TUR.1");
        Expect(s, s.Build(), "TagMockExam:TYT-MAT", "Focus:TUR.1:30");
    }

    [Fact]
    public void S11_a_topic_appears_once_under_its_best_kind()
    {
        var s = Math3().State("TUR.1", trend: TopicTrend.Declining, confidence: TopicConfidence.Reliable, probabilityUp: 0.05)
            .Priority("TUR.1").LowData("TUR.1");
        Expect(s, s.Build(), "Focus:TUR.1:30");
    }

    [Fact]
    public void S12_decline_needs_at_least_reasonable_confidence()
    {
        var s = Math3()
            .State("TUR.1", trend: TopicTrend.Declining, confidence: TopicConfidence.Preliminary, probabilityUp: 0.05)
            .State("MAT.1", trend: TopicTrend.Declining, confidence: TopicConfidence.Reasonable, probabilityUp: 0.1);
        Expect(s, s.Build(), "Decline:MAT.1:20");
    }

    [Fact]
    public void S13_declines_ordered_by_how_likely_the_drop_is()
    {
        var s = Math3()
            .State("TUR.1", trend: TopicTrend.Declining, confidence: TopicConfidence.Reliable, probabilityUp: 0.12)
            .State("FIZ.1", trend: TopicTrend.Declining, confidence: TopicConfidence.Reliable, probabilityUp: 0.02);
        Expect(s, s.Build(), "Decline:FIZ.1:20", "Decline:TUR.1:20");
    }

    [Fact]
    public void S14_heavy_topics_with_little_data_get_a_first_measurement()
    {
        var s = Math3().State("FIZ.1").Priority("FIZ.1").LowData("TUR.1", "MAT.1");
        Expect(s, s.Build(), "Focus:FIZ.1:30", "CollectData:TUR.1:20", "CollectData:MAT.1:20");
    }

    [Fact]
    public void S15_strong_topic_untouched_for_three_weeks_is_reviewed()
    {
        var s = Math3().State("TUR.1", mastery: 0.85, daysAgo: 21).State("MAT.1", mastery: 0.9, daysAgo: 30);
        Expect(s, s.Build(), "Review:MAT.1:15", "Review:TUR.1:15");
    }

    [Fact]
    public void S16_no_review_before_three_weeks_or_below_target_mastery()
    {
        var s = Math3().State("TUR.1", mastery: 0.85, daysAgo: 20).State("MAT.1", mastery: 0.79, daysAgo: 40);
        s.Build().Items.ShouldBeEmpty();
    }

    [Fact]
    public void S17_one_gap_item_for_the_widest_gap()
    {
        var s = Math3().State("TUR.1", mastery: 0.6).Gap("TYT-TUR", 0.16).Gap("TYT-MAT", 0.22);
        Expect(s, s.Build(), "PracticeGap:TYT-MAT");
    }

    [Fact]
    public void S18_not_started_mark_changes_the_wording_not_the_choice()
    {
        var s = Math3().State("TUR.1").Priority("TUR.1").Declared("TUR.1", DeclaredStatus.NotStarted);
        var item = s.Build().Items.Single();
        item.Kind.ShouldBe(Focus);
        item.Text.ShouldStartWith("Konuyu henüz çalışmadım diye işaretledin");
    }

    [Fact]
    public void S19_dismissed_item_is_replaced_by_the_next_candidate()
    {
        var s = Math3().State("TUR.1").State("FIZ.1").Priority("TUR.1", "FIZ.1");
        var first = s.Build();
        var dismissed = s.Dismiss(first.Items[0].Key).Build([.. first.Items]);
        Expect(s, dismissed, "Focus:FIZ.1:30");
    }

    [Fact]
    public void S20_kept_items_stay_first_even_when_the_analysis_moved()
    {
        var before = Math3().State("TUR.1").Priority("TUR.1");
        var kept = before.Build().Items;
        var s = Math3().State("TUR.1").State("MAT.1").Priority("MAT.1", "TUR.1");
        Expect(s, s.Build([.. kept]), "Focus:TUR.1:30", "Focus:MAT.1:30");
    }

    [Fact]
    public void S21_kept_items_count_against_the_subject_cap()
    {
        var before = Math3().State("MAT.1").State("MAT.2").Priority("MAT.1", "MAT.2");
        var kept = before.Build().Items;
        var s = Math3().State("MAT.1").State("MAT.2").State("MAT.3").Priority("MAT.3", "MAT.1", "MAT.2");
        Expect(s, s.Build([.. kept]), "Focus:MAT.1:30", "Focus:MAT.2:30");
    }

    [Fact]
    public void S22_ties_break_on_topic_code()
    {
        var s = Math3()
            .State("KIM.1", trend: TopicTrend.Declining, confidence: TopicConfidence.Reliable, probabilityUp: 0.1)
            .State("FIZ.1", trend: TopicTrend.Declining, confidence: TopicConfidence.Reliable, probabilityUp: 0.1);
        Expect(s, s.Build(), "Decline:FIZ.1:20", "Decline:KIM.1:20");
    }

    [Fact]
    public void S23_topics_outside_the_active_tests_are_ignored()
    {
        var s = Math3().State("TUR.1").State("EDB.1").Priority("EDB.1", "TUR.1");
        Expect(s, s.Build(), "Focus:TUR.1:30");
    }

    [Fact]
    public void S24_mock_only_student_gets_tagging_and_starter_topics()
    {
        var s = new Scenario().Topic("A.1", "A", 5).Topic("B.1", "B", 4).Topic("C.1", "C", 3).Topic("D.1", "D", 2).Pending("E1", 0, "TYT-TUR");
        Expect(s, s.Build(), "TagMockExam:TYT-TUR", "FirstRecord:A.1:20", "FirstRecord:B.1:20", "FirstRecord:C.1:20");
    }

    [Fact]
    public void S25_new_student_measures_low_data_topics_then_gets_starters()
    {
        var s = new Scenario().Topic("A.1", "A", 5).Topic("B.1", "B", 4).Topic("A.2", "A", 3).Topic("C.1", "C", 2).LowData("A.1", "B.1");
        Expect(s, s.Build(), "CollectData:A.1:20", "CollectData:B.1:20", "FirstRecord:A.2:20", "FirstRecord:C.1:20");
    }

    [Fact]
    public void Same_input_gives_the_same_plan()
    {
        var a = Math3().State("TUR.1").State("MAT.1").Priority("MAT.1", "TUR.1").Gap("TYT-MAT", 0.2).Build();
        var b = Math3().State("TUR.1").State("MAT.1").Priority("MAT.1", "TUR.1").Gap("TYT-MAT", 0.2).Build();
        b.Items.ShouldBe(a.Items);
        a.WeekStart.ShouldBe(new DateOnly(2026, 11, 2));
        a.RuleSetVersion.ShouldBe("R2026.1");
    }

    [Fact]
    public void Texts_never_mention_scores_ranks_or_others()
    {
        var s = Math3()
            .State("TUR.1").State("MAT.1", mastery: 0.9, daysAgo: 30)
            .State("FIZ.1", trend: TopicTrend.Declining, confidence: TopicConfidence.Reliable, probabilityUp: 0.1)
            .Priority("TUR.1").LowData("KIM.1").Gap("TYT-FEN", 0.2).Pending("E1", 1, "TYT-TUR");
        var items = s.Build().Items.Concat(Math3().State("TUR.1", mastery: 0.6).Gap("TYT-MAT", 0.2).Build().Items)
            .Concat(new Scenario().Topic("A.1", "A", 5).Build().Items)
            .ToList();
        items.Select(i => i.Kind).Distinct().Count().ShouldBe(7);
        foreach (var text in items.Select(i => $"{i.Title} {i.Text}".ToLowerInvariant()))
        {
            text.ShouldNotContain("puan");
            text.ShouldNotContain("sıra");
            text.ShouldNotContain("diğer öğrenci");
            text.ShouldNotContain("zayıf");
        }
    }
}
