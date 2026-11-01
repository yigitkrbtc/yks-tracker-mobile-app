namespace Nirengi.Modules.Analytics.Engine;

/// <summary>
/// Entry point of the analysis engine (M0–M3, M6, M7 lite, M11). Pure and deterministic: same input and rule set
/// give the same result (ADR-0003). Verified by the golden dataset in tests/Analytics.GoldenTests.
/// </summary>
public static class Analyzer
{
    public static AnalysisResult Analyze(AnalysisInput input, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rules);

        var today = input.Today;
        var catalog = input.Catalog;
        var topics = catalog.Topics.ToDictionary(t => t.Id);
        var order = catalog.Topics.Select((t, i) => (t.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var topicsBySubject = catalog.Topics
            .GroupBy(t => t.SubjectCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(t => t.Id).ToList(), StringComparer.Ordinal);
        var sections = catalog.Sections.ToDictionary(s => s.Code, StringComparer.Ordinal);

        // M0: skip and count what cannot be right.
        var invalid = 0;
        var records = new Dictionary<Guid, List<TopicRecord>>();
        foreach (var r in input.TopicRecords)
        {
            if (!topics.ContainsKey(r.TopicId) || !TopicMetrics.IsValid(r, today))
            {
                invalid++;
                continue;
            }

            if (!records.TryGetValue(r.TopicId, out var list))
            {
                records[r.TopicId] = list = [];
            }

            list.Add(r);
        }

        var effective = records.ToDictionary(x => x.Key, x => TopicMetrics.Effective(x.Value, today, rules));

        // M1 prior: the student's own subject mean from the other topics (leave-one-out), or Beta(1, 1).
        (double A, double B) Prior(Guid id)
        {
            double d = 0, y = 0;
            var rawMarked = 0;
            foreach (var other in topicsBySubject[topics[id].SubjectCode])
            {
                if (other == id || !records.TryGetValue(other, out var list))
                {
                    continue;
                }

                d += effective[other].Correct;
                y += effective[other].Wrong;
                rawMarked += list.Sum(r => r.Correct + r.Wrong);
            }

            if (rawMarked < rules.PriorMinMarked)
            {
                return (1, 1);
            }

            var mean = Math.Clamp(d / (d + y), rules.PriorMeanMin, rules.PriorMeanMax);
            return (rules.PriorStrength * mean, rules.PriorStrength * (1 - mean));
        }

        var analyses = new Dictionary<Guid, TopicAnalysis>();
        var posterior = new Dictionary<Guid, (double A, double B)>();
        foreach (var topic in catalog.Topics)
        {
            var prior = Prior(topic.Id);
            if (!records.TryGetValue(topic.Id, out var list))
            {
                posterior[topic.Id] = prior;
                analyses[topic.Id] = TopicAnalysis.Empty;
                continue;
            }

            var counts = effective[topic.Id];
            posterior[topic.Id] = (prior.A + counts.Correct, prior.B + counts.Wrong);
            analyses[topic.Id] = TopicMetrics.Analyze(list, counts, prior, today, rules);
        }

        // Mock exams: drop what cannot be right, keep date order (input order breaks ties).
        var exams = new List<(MockExamRecord Exam, IReadOnlyList<(CatalogSectionInfo Section, MockSectionRecord Row)> Rows)>();
        foreach (var exam in input.MockExams.OrderBy(m => m.Date))
        {
            if (exam.Date > today)
            {
                invalid++;
                continue;
            }

            var rows = new List<(CatalogSectionInfo, MockSectionRecord)>();
            foreach (var row in exam.Sections)
            {
                if (!sections.TryGetValue(row.Code, out var section)
                    || row.QuestionCount <= 0 || row.Correct < 0 || row.Wrong < 0
                    || row.Correct + row.Wrong > row.QuestionCount)
                {
                    invalid++;
                    continue;
                }

                rows.Add((section, row));
            }

            if (rows.Count > 0)
            {
                exams.Add((exam, rows));
            }
        }

        var sectionAnalyses = new Dictionary<string, SectionAnalysis>(StringComparer.Ordinal);
        foreach (var (code, entries) in MockMetrics.Entries(exams))
        {
            sectionAnalyses[code] = MockMetrics.Analyze(entries, sections[code].QuestionCount, today, rules);
        }

        // M11c.
        var signals = MockMetrics.Signals(exams, topics, topicsBySubject, rules);
        foreach (var (id, signal) in signals)
        {
            analyses[id] = analyses[id] with { MockSignal = signal };
        }

        bool Eligible(Guid id) =>
            analyses[id].ConfidenceLevel is not (ConfidenceLevel.None or ConfidenceLevel.VeryLow)
            && analyses[id].Band is not (MasteryBand.None or MasteryBand.Insufficient);

        // M6 priority.
        foreach (var topic in catalog.Topics)
        {
            if (!Eligible(topic.Id))
            {
                continue;
            }

            var a = analyses[topic.Id];
            var mastery = a.Mastery!.Value;
            var q = topic.ExpectedQuestions;
            var priority = q * Math.Max(0, rules.TargetMastery - mastery) * RuleSet.Learnability(mastery);
            if (a.MockSignal is { Shown: true } s)
            {
                priority += rules.MockSignalWeight * q * Math.Max(0, s.Rate - s.SubjectRate);
            }

            analyses[topic.Id] = a with { Priority = priority };
        }

        var activeTests = input.ActiveTests.ToHashSet(StringComparer.Ordinal);
        var activeSubjects = catalog.Subjects
            .Where(s => s.Tests.Any(activeTests.Contains))
            .Select(s => s.Code)
            .ToHashSet(StringComparer.Ordinal);
        var active = catalog.Topics.Where(t => activeSubjects.Contains(t.SubjectCode)).ToList();

        var priorities = new List<Guid>();
        var perSubject = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var t in active
                     .Where(t => analyses[t.Id].Priority > 0)
                     .OrderByDescending(t => analyses[t.Id].Priority)
                     .ThenByDescending(t => t.ExpectedQuestions)
                     .ThenBy(t => order[t.Id]))
        {
            if (perSubject.GetValueOrDefault(t.SubjectCode) >= rules.PriorityPerSubject)
            {
                continue;
            }

            priorities.Add(t.Id);
            perSubject[t.SubjectCode] = perSubject.GetValueOrDefault(t.SubjectCode) + 1;
            if (priorities.Count == rules.PriorityTop)
            {
                break;
            }
        }

        var lowData = active
            .Where(t => !Eligible(t.Id) && t.ExpectedQuestions > 0)
            .OrderByDescending(t => t.ExpectedQuestions)
            .ThenBy(t => order[t.Id])
            .Take(rules.PriorityLowData)
            .Select(t => t.Id)
            .ToList();

        // M7 lite.
        AggregateAnalysis? Aggregate(IReadOnlyList<Guid> ids)
        {
            var qSum = ids.Sum(t => topics[t].ExpectedQuestions);
            if (qSum <= 0)
            {
                return null;
            }

            double mean = 0, variance = 0, coverage = 0;
            foreach (var t in ids)
            {
                var w = topics[t].ExpectedQuestions / qSum;
                var (a, b) = posterior[t];
                mean += w * BetaMath.Mean(a, b);
                variance += w * w * BetaMath.Variance(a, b);
                if (analyses[t].Confidence > rules.CoverageConfidence)
                {
                    coverage += w;
                }
            }

            var sd = Math.Sqrt(variance);
            return new AggregateAnalysis(
                mean,
                Math.Max(0, mean - (rules.AggregateZ * sd)),
                Math.Min(1, mean + (rules.AggregateZ * sd)),
                coverage,
                ids.Count(records.ContainsKey),
                ids.Count);
        }

        var subjectAnalyses = new Dictionary<string, AggregateAnalysis>(StringComparer.Ordinal);
        foreach (var subject in catalog.Subjects)
        {
            if (Aggregate(topicsBySubject.GetValueOrDefault(subject.Code) ?? []) is { } agg)
            {
                subjectAnalyses[subject.Code] = agg;
            }
        }

        var testAnalyses = new Dictionary<string, AggregateAnalysis>(StringComparer.Ordinal);
        foreach (var test in catalog.Subjects.SelectMany(s => s.Tests).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var ids = catalog.Subjects
                .Where(s => s.Tests.Contains(test, StringComparer.Ordinal))
                .SelectMany(s => topicsBySubject.GetValueOrDefault(s.Code) ?? [])
                .ToList();
            if (Aggregate(ids) is { } agg)
            {
                testAnalyses[test] = agg;
            }
        }

        // M11b practice–mock gap.
        foreach (var code in sectionAnalyses.Keys.ToList())
        {
            var analysis = sectionAnalyses[code];
            var ids = sections[code].SubjectCodes
                .SelectMany(s => topicsBySubject.GetValueOrDefault(s) ?? [])
                .Where(Eligible)
                .ToList();
            if (ids.Count < rules.GapMinTopics || !analysis.IsSufficient)
            {
                continue;
            }

            var qSum = ids.Sum(t => topics[t].ExpectedQuestions);
            if (qSum <= 0)
            {
                continue;
            }

            var practice = ids.Sum(t => topics[t].ExpectedQuestions * analyses[t].Mastery!.Value) / qSum;
            var gap = practice - analysis.Accuracy;
            sectionAnalyses[code] = analysis with { PracticeGap = gap, GapWarning = gap > rules.GapThreshold };
        }

        return new AnalysisResult(
            rules.Version,
            invalid,
            analyses,
            subjectAnalyses,
            testAnalyses,
            sectionAnalyses,
            priorities,
            lowData);
    }
}
