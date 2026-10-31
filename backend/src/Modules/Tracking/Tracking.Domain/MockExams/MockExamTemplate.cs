using Nirengi.BuildingBlocks.Domain.Results;

namespace Nirengi.Modules.Tracking.Domain.MockExams;

/// <summary>A line of the score sheet template, as Curriculum publishes it.</summary>
/// <param name="Session">"TYT" or "AYT".</param>
/// <param name="SubjectCodes">Subjects whose topics may be marked in this section.</param>
public sealed record MockSectionDefinition(
    string Code,
    string TestCode,
    string Session,
    IReadOnlyList<string> SubjectCodes,
    int QuestionCount,
    string? ParentCode,
    string? AlternativeOf);

/// <summary>Checks a mock exam against the score sheet template; pure, so the rules are unit tested without a database.</summary>
public static class MockExamTemplate
{
    /// <param name="topicSubjects">Subject code of every marked topic that exists; a missing one is unknown.</param>
    /// <returns>Null when the exam fits the template.</returns>
    public static Error? Check(
        MockExamData data,
        IReadOnlyDictionary<string, MockSectionDefinition> template,
        IReadOnlyDictionary<Guid, string> topicSubjects)
    {
        var defs = new List<MockSectionDefinition>(data.Sections.Count);
        foreach (var section in data.Sections)
        {
            if (!template.TryGetValue(section.SectionCode, out var def))
            {
                return TrackingErrors.UnknownMockSection;
            }

            defs.Add(def);
        }

        var sessionOk = data.Kind switch
        {
            MockExamKind.Tyt => defs.All(d => d.Session == "TYT"),
            MockExamKind.Ayt => defs.All(d => d.Session == "AYT"),
            _ => defs.Count == 1,
        };
        if (!sessionOk)
        {
            return TrackingErrors.MockSectionsDoNotFitKind;
        }

        // The ÖSYM count for TYT/AYT; a branch exam keeps the publisher's own count.
        if (data.Kind != MockExamKind.Branch && data.Sections.Zip(defs).Any(p => p.First.QuestionCount != p.Second.QuestionCount))
        {
            return TrackingErrors.MockSectionQuestionCount;
        }

        if (data.Sections.Any(s => s.Correct + s.Wrong > s.QuestionCount))
        {
            return TrackingErrors.MockSectionCounts;
        }

        var codes = defs.Select(d => d.Code).ToHashSet(StringComparer.Ordinal);
        if (defs.Any(d => (d.ParentCode is not null && codes.Contains(d.ParentCode))
                          || (d.AlternativeOf is not null && codes.Contains(d.AlternativeOf))))
        {
            return TrackingErrors.MockSectionsOverlap;
        }

        var sections = data.Sections.ToDictionary(s => s.SectionCode, StringComparer.Ordinal);
        foreach (var group in data.Marks.GroupBy(m => m.SectionCode, StringComparer.Ordinal))
        {
            if (!sections.TryGetValue(group.Key, out var s)
                || group.Sum(m => m.Wrong) > s.Wrong
                || group.Sum(m => m.Blank) > s.QuestionCount - s.Correct - s.Wrong)
            {
                return TrackingErrors.MarksExceedSection;
            }
        }

        foreach (var mark in data.Marks)
        {
            if (!topicSubjects.TryGetValue(mark.TopicId, out var subject))
            {
                return TrackingErrors.UnknownTopic;
            }

            if (!template[mark.SectionCode].SubjectCodes.Contains(subject))
            {
                return TrackingErrors.TopicNotInSection;
            }
        }

        return null;
    }
}
