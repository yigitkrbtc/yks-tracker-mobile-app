using Nirengi.BuildingBlocks.Domain.Entities;

namespace Nirengi.Modules.Curriculum.Domain.Exams;

/// <summary>One timed test of a session (e.g. AYT Sosyal Bilimler-2); the id is its stable code.</summary>
public sealed class ExamTest : Entity<string>
{
    public const int CodeMaxLength = 20;

    public const int NameMaxLength = 100;

    private ExamTest()
    {
    }

    public ExamSession Session { get; private set; }

    public string Name { get; private set; } = null!;

    public int QuestionCount { get; private set; }

    public int SortOrder { get; private set; }

    public static ExamTest Create(string code, ExamSession session, string name, int questionCount, int sortOrder)
    {
        var test = new ExamTest { Id = code };
        test.Update(session, name, questionCount, sortOrder);
        return test;
    }

    public void Update(ExamSession session, string name, int questionCount, int sortOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(questionCount);
        Session = session;
        Name = name;
        QuestionCount = questionCount;
        SortOrder = sortOrder;
    }
}
