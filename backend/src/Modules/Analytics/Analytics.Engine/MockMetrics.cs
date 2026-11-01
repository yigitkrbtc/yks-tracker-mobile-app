namespace Nirengi.Modules.Analytics.Engine;

/// <summary>One mock exam's result for one section key (a test root or a subject line).</summary>
internal readonly record struct SectionEntry(int QuestionCount, int Correct, int Wrong, DateOnly Date)
{
    public int Blank => QuestionCount - Correct - Wrong;

    public double Accuracy => Correct + Wrong > 0 ? Correct / (double)(Correct + Wrong) : 0;
}

/// <summary>M11a (section form) and M11c (topic miss signal).</summary>
internal static class MockMetrics
{
    /// <summary>
    /// Builds the entries per section key. The test root gets an entry from every exam that solved it, either as
    /// its total or as the sum of its subject lines; a subject line only from exams that entered it.
    /// </summary>
    public static Dictionary<string, List<SectionEntry>> Entries(
        IReadOnlyList<(MockExamRecord Exam, IReadOnlyList<(CatalogSectionInfo Section, MockSectionRecord Row)> Rows)> exams)
    {
        var entries = new Dictionary<string, List<SectionEntry>>(StringComparer.Ordinal);
        foreach (var (exam, rows) in exams)
        {
            var solved = rows.Select(x => x.Section.Code).ToHashSet(StringComparer.Ordinal);
            var roots = new Dictionary<string, (int N, int C, int W)>(StringComparer.Ordinal);
            foreach (var (section, row) in rows)
            {
                if (section.ParentCode is null)
                {
                    roots[section.Code] = (row.QuestionCount, row.Correct, row.Wrong);
                }
            }

            foreach (var (section, row) in rows)
            {
                if (section.ParentCode is null)
                {
                    continue;
                }

                Add(entries, section.Code, new SectionEntry(row.QuestionCount, row.Correct, row.Wrong, exam.Date));
                if (!solved.Contains(section.ParentCode))
                {
                    var sum = roots.GetValueOrDefault(section.ParentCode);
                    roots[section.ParentCode] = (sum.N + row.QuestionCount, sum.C + row.Correct, sum.W + row.Wrong);
                }
            }

            foreach (var (code, (n, c, w)) in roots)
            {
                Add(entries, code, new SectionEntry(n, c, w, exam.Date));
            }
        }

        return entries;
    }

    private static void Add(Dictionary<string, List<SectionEntry>> entries, string code, SectionEntry entry)
    {
        if (!entries.TryGetValue(code, out var list))
        {
            entries[code] = list = [];
        }

        list.Add(entry);
    }

    /// <summary>M11a for one key; <paramref name="entries"/> are in date order.</summary>
    public static SectionAnalysis Analyze(List<SectionEntry> entries, int templateQuestions, DateOnly today, RuleSet rules)
    {
        double d = 0, y = 0, b = 0;
        foreach (var e in entries)
        {
            var w = rules.Decay(today.DayNumber - e.Date.DayNumber);
            d += w * e.Correct;
            y += w * e.Wrong;
            b += w * e.Blank;
        }

        var tail = (1 - rules.MockInterval) / 2;
        var accuracy = BetaMath.Mean(1 + d, 1 + y);
        var low = BetaMath.Quantile(tail, 1 + d, 1 + y);
        var high = BetaMath.Quantile(1 - tail, 1 + d, 1 + y);
        var marking = (1 + d + y) / (2 + d + y + b);
        double Net(double acc) => templateQuestions * marking * ((1.25 * acc) - 0.25);

        return new SectionAnalysis(
            Exams: entries.Count,
            IsSufficient: entries.Count >= rules.MockMinExams,
            Accuracy: accuracy,
            AccuracyLow: low,
            AccuracyHigh: high,
            MarkingRate: marking,
            ExpectedNet: Net(accuracy),
            ExpectedNetLow: Net(low),
            ExpectedNetHigh: Net(high),
            Trend: Trend(entries, rules),
            PracticeGap: null,
            GapWarning: false);
    }

    /// <summary>Last N exams pooled vs the ones before; a single outlier exam cannot make a trend.</summary>
    private static TrendResult Trend(List<SectionEntry> entries, RuleSet rules)
    {
        if (entries.Count < rules.MockTrendRecent + rules.MockTrendMinPrevious)
        {
            return TrendResult.Insufficient;
        }

        var recent = entries.Skip(entries.Count - rules.MockTrendRecent).ToList();
        var before = entries.Take(entries.Count - rules.MockTrendRecent).ToList();
        var previous = before.Skip(Math.Max(0, before.Count - rules.MockTrendPrevious)).ToList();

        double rd = recent.Sum(e => e.Correct), ry = recent.Sum(e => e.Wrong);
        double pd = previous.Sum(e => e.Correct), py = previous.Sum(e => e.Wrong);
        var probabilityUp = BetaMath.ProbabilityGreater(1 + rd, 1 + ry, 1 + pd, 1 + py);
        var previousMean = BetaMath.Mean(1 + pd, 1 + py);
        var difference = BetaMath.Mean(1 + rd, 1 + ry) - previousMean;
        var label = rules.Classify(probabilityUp, difference);

        var below = recent.Count(e => e.Accuracy <= previousMean - rules.TrendMinDifference);
        var above = recent.Count(e => e.Accuracy >= previousMean + rules.TrendMinDifference);
        if ((label == TrendLabel.Declining && below < rules.MockTrendMinAgreeing)
            || (label == TrendLabel.Improving && above < rules.MockTrendMinAgreeing))
        {
            label = TrendLabel.Stable;
        }

        return new TrendResult(label, probabilityUp, difference);
    }

    /// <summary>
    /// M11c over fully tagged sections of the last N exams that have one. Exposure of a topic in a section is
    /// q_i scaled to the section's question count over the q of the subjects the section asks.
    /// </summary>
    public static Dictionary<Guid, MockSignal> Signals(
        IReadOnlyList<(MockExamRecord Exam, IReadOnlyList<(CatalogSectionInfo Section, MockSectionRecord Row)> Rows)> exams,
        IReadOnlyDictionary<Guid, CatalogTopicInfo> topics,
        IReadOnlyDictionary<string, List<Guid>> topicsBySubject,
        RuleSet rules)
    {
        var tagged = exams.Where(x => x.Rows.Any(r => r.Row.IsFullyTagged)).ToList();
        tagged = tagged.Skip(Math.Max(0, tagged.Count - rules.SignalLookbackExams)).ToList();

        var exposure = new Dictionary<Guid, double>();
        var missed = new Dictionary<Guid, (int Wrong, int Blank)>();
        var covering = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var (exam, rows) in tagged)
        {
            foreach (var (section, row) in rows)
            {
                if (!row.IsFullyTagged)
                {
                    continue;
                }

                var sectionTopics = section.SubjectCodes
                    .SelectMany(s => topicsBySubject.GetValueOrDefault(s) ?? [])
                    .ToList();
                var qSum = sectionTopics.Sum(t => topics[t].ExpectedQuestions);
                if (qSum <= 0)
                {
                    continue;
                }

                foreach (var t in sectionTopics)
                {
                    exposure[t] = exposure.GetValueOrDefault(t) + (topics[t].ExpectedQuestions * row.QuestionCount / qSum);
                    if (!covering.TryGetValue(t, out var set))
                    {
                        covering[t] = set = [];
                    }

                    set.Add(exam.Id);
                }

                foreach (var mark in row.Marks)
                {
                    if (topics.ContainsKey(mark.TopicId))
                    {
                        var m = missed.GetValueOrDefault(mark.TopicId);
                        missed[mark.TopicId] = (m.Wrong + mark.Wrong, m.Blank + mark.Blank);
                    }
                }
            }
        }

        static double Rate(double miss, double exposed) => (1 + miss) / (2 + miss + Math.Max(exposed - miss, 0));

        var subjectRate = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (subject, ids) in topicsBySubject)
        {
            var e = ids.Sum(t => exposure.GetValueOrDefault(t));
            if (e > 0)
            {
                var m = ids.Sum(t => missed.GetValueOrDefault(t) is var x ? x.Wrong + x.Blank : 0);
                subjectRate[subject] = Rate(m, e);
            }
        }

        var signals = new Dictionary<Guid, MockSignal>();
        foreach (var (t, e) in exposure)
        {
            var (wrong, blank) = missed.GetValueOrDefault(t);
            var miss = wrong + blank;
            signals[t] = new MockSignal(
                Exposure: e,
                Missed: miss,
                Wrong: wrong,
                Blank: blank,
                Exams: covering[t].Count,
                Rate: Rate(miss, e),
                SubjectRate: subjectRate[topics[t].SubjectCode],
                Shown: e >= rules.SignalMinExposure);
        }

        return signals;
    }
}
