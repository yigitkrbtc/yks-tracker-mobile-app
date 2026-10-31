namespace Nirengi.Modules.Tracking.Domain.TopicTests;

/// <summary>Answers for one topic within a <see cref="TopicTest"/>. User and date are copied for analysis queries.</summary>
public sealed class TopicResult
{
    private TopicResult()
    {
    }

    public Guid TopicTestId { get; private set; }

    public Guid TopicId { get; private set; }

    public Guid UserId { get; private set; }

    public DateOnly StudiedOn { get; private set; }

    public int Total { get; private set; }

    public int Correct { get; private set; }

    public int Wrong { get; private set; }

    public int Blank { get; private set; }

    /// <summary>Correct − Wrong / 4; may be negative.</summary>
    public decimal Net { get; private set; }

    public int? MistakeKnowledge { get; private set; }

    public int? MistakeAttention { get; private set; }

    public int? MistakeTime { get; private set; }

    internal static TopicResult Create(TopicTest test, TopicResultData data)
    {
        var result = new TopicResult { TopicTestId = test.Id, TopicId = data.TopicId };
        result.Update(test, data);
        return result;
    }

    internal void Update(TopicTest test, TopicResultData data)
    {
        UserId = test.UserId;
        StudiedOn = test.StudiedOn;
        Total = data.Total;
        Correct = data.Correct;
        Wrong = data.Wrong;
        Blank = data.Blank;
        Net = TopicTestRules.Net(data.Correct, data.Wrong);
        MistakeKnowledge = data.MistakeKnowledge;
        MistakeAttention = data.MistakeAttention;
        MistakeTime = data.MistakeTime;
    }
}
