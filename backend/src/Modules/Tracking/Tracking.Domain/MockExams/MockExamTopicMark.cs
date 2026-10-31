namespace Nirengi.Modules.Tracking.Domain.MockExams;

/// <summary>Lost questions of one section put on one topic (M11c). User and date are copied for analysis queries.</summary>
public sealed class MockExamTopicMark
{
    private MockExamTopicMark()
    {
    }

    public Guid MockExamId { get; private set; }

    public string SectionCode { get; private set; } = null!;

    public Guid TopicId { get; private set; }

    public Guid UserId { get; private set; }

    public DateOnly TakenOn { get; private set; }

    public int Wrong { get; private set; }

    public int Blank { get; private set; }

    public int? MistakeKnowledge { get; private set; }

    public int? MistakeAttention { get; private set; }

    public int? MistakeTime { get; private set; }

    internal static MockExamTopicMark Create(MockExam exam, TopicMarkData data)
    {
        var mark = new MockExamTopicMark { MockExamId = exam.Id, SectionCode = data.SectionCode, TopicId = data.TopicId };
        mark.Update(exam, data);
        return mark;
    }

    internal void Update(MockExam exam, TopicMarkData data)
    {
        UserId = exam.UserId;
        TakenOn = exam.TakenOn;
        Wrong = data.Wrong;
        Blank = data.Blank;
        MistakeKnowledge = data.MistakeKnowledge;
        MistakeAttention = data.MistakeAttention;
        MistakeTime = data.MistakeTime;
    }
}
