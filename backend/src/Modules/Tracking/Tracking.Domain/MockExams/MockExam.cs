using Nirengi.BuildingBlocks.Domain.Entities;

namespace Nirengi.Modules.Tracking.Domain.MockExams;

/// <summary>
/// A mock exam (TYT, AYT or a single branch) entered per section, recorded offline-first like a topic test: client
/// UUID v7, last writer by edit time wins, soft delete with a tombstone (ADR-0005, ADR-0014). Its answers never
/// reach topic mastery (ADR-0010); only the optional topic marks feed the exam miss signal (M11c).
/// </summary>
public sealed class MockExam : AggregateRoot<Guid>, ISyncRecord
{
    private readonly List<MockExamSection> _sections = [];
    private readonly List<MockExamTopicMark> _marks = [];

    private MockExam()
    {
    }

    public Guid UserId { get; private set; }

    public MockExamKind Kind { get; private set; }

    public DateOnly TakenOn { get; private set; }

    public Guid? SourceId { get; private set; }

    public string? SourceName { get; private set; }

    public string? Title { get; private set; }

    public int? DurationMinutes { get; private set; }

    public string? Note { get; private set; }

    /// <summary>When the client last edited or deleted it, capped at server time; decides which write wins.</summary>
    public DateTimeOffset ClientModifiedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Position in the change feed (GET /v1/sync); assigned by persistence on every write.</summary>
    public long ChangeSeq { get; private set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public IReadOnlyList<MockExamSection> Sections => _sections;

    public IReadOnlyList<MockExamTopicMark> Marks => _marks;

    public static MockExam Create(Guid id, Guid userId, MockExamData data, DateTimeOffset clientModifiedAt, DateTimeOffset now)
    {
        var exam = new MockExam { Id = id, UserId = userId, CreatedAt = now };
        exam.Set(data, EditTime.Of(clientModifiedAt, now), now);
        return exam;
    }

    /// <summary>Replaces the content unless an equal or newer edit is already stored.</summary>
    /// <returns>False when nothing changed (a replay or a stale write); the stored state stands.</returns>
    public bool Apply(MockExamData data, DateTimeOffset clientModifiedAt, DateTimeOffset now)
    {
        var editTime = EditTime.Of(clientModifiedAt, now);
        if (editTime <= ClientModifiedAt)
        {
            return false;
        }

        DeletedAt = null;
        Set(data, editTime, now);
        return true;
    }

    /// <returns>False when already deleted or a newer edit is stored.</returns>
    public bool Delete(DateTimeOffset clientModifiedAt, DateTimeOffset now)
    {
        var editTime = EditTime.Of(clientModifiedAt, now);
        if (IsDeleted || editTime <= ClientModifiedAt)
        {
            return false;
        }

        _marks.Clear();
        _sections.Clear();
        ClientModifiedAt = editTime;
        DeletedAt = now;
        UpdatedAt = now;
        return true;
    }

    public void AssignChangeSeq(long changeSeq) => ChangeSeq = changeSeq;

    private void Set(MockExamData data, DateTimeOffset editTime, DateTimeOffset now)
    {
        Guard(data);
        Kind = data.Kind;
        TakenOn = data.TakenOn;
        SourceId = data.SourceId;
        SourceName = data.SourceId.HasValue ? null : data.SourceName!.Trim();
        Title = string.IsNullOrWhiteSpace(data.Title) ? null : data.Title.Trim();
        DurationMinutes = data.DurationMinutes;
        Note = string.IsNullOrWhiteSpace(data.Note) ? null : data.Note.Trim();
        ClientModifiedAt = editTime;
        UpdatedAt = now;

        var tagged = data.Marks
            .GroupBy(m => m.SectionCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Wrong + m.Blank), StringComparer.Ordinal);

        // Update rows in place (keyed by section, and section + topic) instead of delete-and-insert.
        var sections = data.Sections.ToDictionary(s => s.SectionCode, StringComparer.Ordinal);
        _sections.RemoveAll(s => !sections.ContainsKey(s.SectionCode));
        foreach (var section in _sections)
        {
            section.Update(this, sections[section.SectionCode], tagged.GetValueOrDefault(section.SectionCode));
            sections.Remove(section.SectionCode);
        }

        _sections.AddRange(sections.Values.Select(s => MockExamSection.Create(this, s, tagged.GetValueOrDefault(s.SectionCode))));

        var marks = data.Marks.ToDictionary(m => (m.SectionCode, m.TopicId));
        _marks.RemoveAll(m => !marks.ContainsKey((m.SectionCode, m.TopicId)));
        foreach (var mark in _marks)
        {
            mark.Update(this, marks[(mark.SectionCode, mark.TopicId)]);
            marks.Remove((mark.SectionCode, mark.TopicId));
        }

        _marks.AddRange(marks.Values.Select(m => MockExamTopicMark.Create(this, m)));
    }

    /// <summary>The validator and the template check reject bad input first; these guards only catch programming errors.</summary>
    private static void Guard(MockExamData data)
    {
        if (!Enum.IsDefined(data.Kind))
        {
            throw new ArgumentException("Unknown mock exam kind.", nameof(data));
        }

        if (data.Sections.Count is 0 or > MockExamRules.MaxSections || data.Marks.Count > MockExamRules.MaxMarks)
        {
            throw new ArgumentException($"A mock exam has 1 to {MockExamRules.MaxSections} sections and at most {MockExamRules.MaxMarks} topic marks.", nameof(data));
        }

        if (data.SourceId.HasValue == !string.IsNullOrWhiteSpace(data.SourceName))
        {
            throw new ArgumentException("Exactly one of source id and source name is required.", nameof(data));
        }

        var sections = new Dictionary<string, MockSectionData>(StringComparer.Ordinal);
        foreach (var s in data.Sections)
        {
            if (s.QuestionCount is < 1 or > MockExamRules.MaxBranchQuestions
                || s.Correct < 0 || s.Wrong < 0 || s.Correct + s.Wrong > s.QuestionCount
                || !sections.TryAdd(s.SectionCode, s))
            {
                throw new ArgumentException($"Invalid or repeated section {s.SectionCode}.", nameof(data));
            }
        }

        if (data.Marks.Select(m => (m.SectionCode, m.TopicId)).Distinct().Count() != data.Marks.Count)
        {
            throw new ArgumentException("A topic is marked twice in one section.", nameof(data));
        }

        foreach (var m in data.Marks)
        {
            var reasons = (m.MistakeKnowledge ?? 0) + (m.MistakeAttention ?? 0) + (m.MistakeTime ?? 0);
            if (!sections.ContainsKey(m.SectionCode)
                || m.Wrong < 0 || m.Blank < 0 || m.Wrong + m.Blank == 0
                || m.MistakeKnowledge < 0 || m.MistakeAttention < 0 || m.MistakeTime < 0
                || reasons > m.Wrong)
            {
                throw new ArgumentException($"Invalid topic mark {m.TopicId} in section {m.SectionCode}.", nameof(data));
            }
        }

        foreach (var group in data.Marks.GroupBy(m => m.SectionCode, StringComparer.Ordinal))
        {
            var s = sections[group.Key];
            if (group.Sum(m => m.Wrong) > s.Wrong || group.Sum(m => m.Blank) > s.QuestionCount - s.Correct - s.Wrong)
            {
                throw new ArgumentException($"Section {group.Key} has more marked than lost questions.", nameof(data));
            }
        }
    }
}
