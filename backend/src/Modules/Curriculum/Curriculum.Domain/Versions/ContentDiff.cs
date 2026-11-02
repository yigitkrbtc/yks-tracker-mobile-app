namespace Nirengi.Modules.Curriculum.Domain.Versions;

/// <summary>
/// The operations that turn <c>live</c> into <c>desired</c> (ADR-0019). Used for a seed import (desired = the
/// shipped seed) and for going back to an earlier version (desired = its snapshot). Never deletes: a topic or
/// source missing from <c>desired</c> is retired; calendars missing from it are left alone.
/// </summary>
public static class ContentDiff
{
    public static IReadOnlyList<CurriculumOperation> Between(CurriculumContent live, CurriculumContent desired)
    {
        var ops = new List<CurriculumOperation>();
        var liveTopics = live.Topics.ToDictionary(t => t.Code, StringComparer.Ordinal);
        var wanted = desired.Topics.ToDictionary(t => t.Code, StringComparer.Ordinal);

        foreach (var d in desired.Topics)
        {
            if (!liveTopics.TryGetValue(d.Code, out var l))
            {
                ops.Add(new AddTopic(d.Code, d.SubjectCode, d.Name, d.Subtopics, d.GradeMin, d.GradeMax, d.Description, d.SortOrder));
                if (!d.IsActive)
                {
                    ops.Add(new RetireTopic(d.Code));
                }

                continue;
            }

            if (d.IsActive && !l.IsActive)
            {
                ops.Add(new ReactivateTopic(d.Code));
            }

            if (l.Name != d.Name || !l.Subtopics.SequenceEqual(d.Subtopics) || l.GradeMin != d.GradeMin || l.GradeMax != d.GradeMax
                || l.Description != d.Description || l.SortOrder != d.SortOrder)
            {
                ops.Add(new UpdateTopic(d.Code, d.Name, d.Subtopics, d.GradeMin, d.GradeMax, d.Description, d.SortOrder));
            }

            if (!d.IsActive && l.IsActive)
            {
                ops.Add(new RetireTopic(d.Code));
            }
        }

        ops.AddRange(live.Topics.Where(l => l.IsActive && !wanted.ContainsKey(l.Code)).Select(l => new RetireTopic(l.Code)));

        var liveSources = live.Sources.ToDictionary(s => s.Code, StringComparer.Ordinal);
        var wantedSources = desired.Sources.Select(s => s.Code).ToHashSet(StringComparer.Ordinal);
        foreach (var d in desired.Sources)
        {
            if (!liveSources.TryGetValue(d.Code, out var l))
            {
                ops.Add(new AddSource(d.Code, d.Name, d.Kind));
                if (!d.IsActive)
                {
                    ops.Add(new RetireSource(d.Code));
                }

                continue;
            }

            if (d.IsActive && !l.IsActive)
            {
                ops.Add(new ReactivateSource(d.Code));
            }

            if (l.Name != d.Name || l.Kind != d.Kind)
            {
                ops.Add(new UpdateSource(d.Code, d.Name, d.Kind));
            }

            if (!d.IsActive && l.IsActive)
            {
                ops.Add(new RetireSource(d.Code));
            }
        }

        ops.AddRange(live.Sources.Where(l => l.IsActive && !wantedSources.Contains(l.Code)).Select(l => new RetireSource(l.Code)));

        var liveCalendars = live.Calendars.ToDictionary(c => c.ExamYear);
        foreach (var d in desired.Calendars)
        {
            if (!liveCalendars.TryGetValue(d.ExamYear, out var l) || !SameCalendar(l, d))
            {
                ops.Add(new SetCalendar(d));
            }
        }

        return ops;
    }

    private static bool SameCalendar(Calendar.ExamCalendar a, Calendar.ExamCalendar b) =>
        a.SourceUrl == b.SourceUrl && a.Note == b.Note && a.Events.SequenceEqual(b.Events);
}
