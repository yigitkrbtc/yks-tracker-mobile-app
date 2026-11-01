namespace Nirengi.Modules.Analytics.Engine;

/// <summary>Decayed, repeat-weighted counts of a topic (M1 inputs).</summary>
internal readonly record struct EffectiveCounts(double Correct, double Wrong, double Blank)
{
    public double Marked => Correct + Wrong;

    public double Questions => Correct + Wrong + Blank;
}

/// <summary>M0–M3 for a single topic.</summary>
internal static class TopicMetrics
{
    /// <summary>M0: counts add up, nothing negative, not in the future.</summary>
    public static bool IsValid(TopicRecord r, DateOnly today) =>
        r.Total > 0 && r.Correct >= 0 && r.Wrong >= 0 && r.Blank >= 0
        && r.Correct + r.Wrong + r.Blank == r.Total
        && r.Date <= today;

    public static EffectiveCounts Effective(IReadOnlyList<TopicRecord> records, DateOnly today, RuleSet rules)
    {
        double d = 0, y = 0, b = 0;
        foreach (var r in records)
        {
            var w = rules.Decay(today.DayNumber - r.Date.DayNumber) * (r.IsRepeat ? rules.RepeatWeight : 1);
            d += w * r.Correct;
            y += w * r.Wrong;
            b += w * r.Blank;
        }

        return new EffectiveCounts(d, y, b);
    }

    public static TopicAnalysis Analyze(
        IReadOnlyList<TopicRecord> records,
        EffectiveCounts counts,
        (double A, double B) prior,
        DateOnly today,
        RuleSet rules)
    {
        var a = prior.A + counts.Correct;
        var b = prior.B + counts.Wrong;
        var tail = (1 - rules.Interval) / 2;
        var mastery = BetaMath.Mean(a, b);
        var marking = (1 + counts.Marked) / (2 + counts.Questions);

        var dispersion = Dispersion(records, rules);
        var last = records.Max(r => r.Date);
        var adjusted = counts.Questions / dispersion;
        var confidence = adjusted / (adjusted + rules.ConfidenceK)
            * Math.Pow(0.5, (today.DayNumber - last.DayNumber) / rules.FreshnessHalfLifeDays);
        var distinctDates = records.Select(r => r.Date).Distinct().Count();

        var totals = new TopicTotals(
            records.Count,
            records.Sum(r => r.Total),
            records.Sum(r => r.Correct),
            records.Sum(r => r.Wrong),
            records.Sum(r => r.Blank));

        return new TopicAnalysis(
            Mastery: mastery,
            MasteryLow: BetaMath.Quantile(tail, a, b),
            MasteryHigh: BetaMath.Quantile(1 - tail, a, b),
            MarkingRate: marking,
            Optimistic: marking < rules.OptimisticMarking,
            ExpectedNetPerQuestion: marking * ((1.25 * mastery) - 0.25),
            EffectiveQuestions: counts.Questions,
            Dispersion: dispersion,
            Confidence: confidence,
            ConfidenceLevel: Level(confidence, distinctDates, rules),
            Band: Band(mastery, totals, rules),
            Trend: Trend(records, today, rules),
            Priority: null,
            Totals: totals,
            MockSignal: null);
    }

    /// <summary>φ: Pearson dispersion of record accuracies around the pooled rate, clamped to [1, 3].</summary>
    private static double Dispersion(IReadOnlyList<TopicRecord> records, RuleSet rules)
    {
        var marked = records.Where(r => r.Correct + r.Wrong > 0).ToList();
        var phi = 1.0;
        if (marked.Count >= 2)
        {
            double pooled = marked.Sum(r => r.Correct) / (double)marked.Sum(r => r.Correct + r.Wrong);
            if (pooled is > 0 and < 1)
            {
                var chi = 0.0;
                foreach (var r in marked)
                {
                    var n = r.Correct + r.Wrong;
                    var dev = r.Correct - (n * pooled);
                    chi += dev * dev / (n * pooled * (1 - pooled));
                }

                phi = chi / (marked.Count - 1);
            }
        }

        return Math.Clamp(phi, rules.DispersionMin, rules.DispersionMax);
    }

    private static ConfidenceLevel Level(double confidence, int distinctDates, RuleSet rules)
    {
        if (confidence < rules.PreliminaryFrom)
        {
            return ConfidenceLevel.VeryLow;
        }

        if (confidence < rules.ReasonableFrom)
        {
            return ConfidenceLevel.Preliminary;
        }

        return confidence < rules.ReliableFrom || distinctDates < 2 ? ConfidenceLevel.Reasonable : ConfidenceLevel.Reliable;
    }

    private static MasteryBand Band(double mastery, TopicTotals totals, RuleSet rules)
    {
        if (totals.Questions < rules.BandMinQuestions || totals.Records < rules.BandMinRecords)
        {
            return MasteryBand.Insufficient;
        }

        if (mastery < rules.WeakBelow)
        {
            return MasteryBand.Weak;
        }

        return mastery < rules.StrongFrom ? MasteryBand.Developing : MasteryBand.Strong;
    }

    /// <summary>M3: recent window vs previous window, raw (repeat-weighted, not decayed) counts.</summary>
    private static TrendResult Trend(IReadOnlyList<TopicRecord> records, DateOnly today, RuleSet rules)
    {
        var recent = records.Where(r => today.DayNumber - r.Date.DayNumber < rules.TrendRecentDays).ToList();
        var previous = records.Where(r =>
        {
            var age = today.DayNumber - r.Date.DayNumber;
            return age >= rules.TrendRecentDays && age < rules.TrendRecentDays + rules.TrendPreviousDays;
        }).ToList();
        var window = recent.Concat(previous).ToList();
        if (window.Count == 0)
        {
            return TrendResult.Insufficient;
        }

        var span = window.Max(r => r.Date).DayNumber - window.Min(r => r.Date).DayNumber;
        if (window.Count < rules.TrendMinRecords
            || window.Sum(r => r.Total) < rules.TrendMinQuestions
            || span < rules.TrendMinSpanDays
            || recent.Sum(r => r.Total) < rules.TrendMinRecentQuestions
            || recent.Count < rules.TrendMinRecentRecords
            || previous.Count < rules.TrendMinPreviousRecords)
        {
            return TrendResult.Insufficient;
        }

        var (rd, ry) = Counts(recent, rules);
        var (pd, py) = Counts(previous, rules);
        var probabilityUp = BetaMath.ProbabilityGreater(1 + rd, 1 + ry, 1 + pd, 1 + py);
        var difference = BetaMath.Mean(1 + rd, 1 + ry) - BetaMath.Mean(1 + pd, 1 + py);
        return new TrendResult(rules.Classify(probabilityUp, difference), probabilityUp, difference);
    }

    private static (double Correct, double Wrong) Counts(List<TopicRecord> records, RuleSet rules)
    {
        double d = 0, y = 0;
        foreach (var r in records)
        {
            var w = r.IsRepeat ? rules.RepeatWeight : 1;
            d += w * r.Correct;
            y += w * r.Wrong;
        }

        return (d, y);
    }
}
