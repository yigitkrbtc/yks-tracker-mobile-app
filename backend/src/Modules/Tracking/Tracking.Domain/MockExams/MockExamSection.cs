namespace Nirengi.Modules.Tracking.Domain.MockExams;

/// <summary>One solved section of a <see cref="MockExam"/>. User and date are copied for analysis queries (M11a).</summary>
public sealed class MockExamSection
{
    private MockExamSection()
    {
    }

    public Guid MockExamId { get; private set; }

    public string SectionCode { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public DateOnly TakenOn { get; private set; }

    /// <summary>Copied from the template when saved (or the student's own count for a branch exam).</summary>
    public int QuestionCount { get; private set; }

    public int Correct { get; private set; }

    public int Wrong { get; private set; }

    public int Blank { get; private set; }

    /// <summary>Correct − Wrong / 4; may be negative.</summary>
    public decimal Net { get; private set; }

    public TaggingStatus TaggingStatus { get; private set; }

    internal static MockExamSection Create(MockExam exam, MockSectionData data, int tagged)
    {
        var section = new MockExamSection { MockExamId = exam.Id, SectionCode = data.SectionCode };
        section.Update(exam, data, tagged);
        return section;
    }

    internal void Update(MockExam exam, MockSectionData data, int tagged)
    {
        UserId = exam.UserId;
        TakenOn = exam.TakenOn;
        QuestionCount = data.QuestionCount;
        Correct = data.Correct;
        Wrong = data.Wrong;
        Blank = data.QuestionCount - data.Correct - data.Wrong;
        Net = MockExamRules.Net(data.Correct, data.Wrong);
        TaggingStatus = MockExamRules.Tagging(Wrong + Blank, tagged);
    }
}
