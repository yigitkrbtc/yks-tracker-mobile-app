using Nirengi.BuildingBlocks.Domain.Entities;

namespace Nirengi.Modules.Curriculum.Domain.Exams;

/// <summary>
/// A subject within a session (e.g. AYT Tarih). It can be asked in more than one test: AYT history and
/// geography appear in both TDE-SB1 and SB2, so a student of either score type sees the whole subject.
/// </summary>
public sealed class Subject : Entity<string>
{
    public const int CodeMaxLength = 20;

    public const int NameMaxLength = 100;

    private Subject()
    {
    }

    public ExamSession Session { get; private set; }

    public string Name { get; private set; } = null!;

    public List<string> TestCodes { get; private set; } = [];

    public int SortOrder { get; private set; }

    public static Subject Create(string code, ExamSession session, string name, IEnumerable<string> testCodes, int sortOrder)
    {
        var subject = new Subject { Id = code };
        subject.Update(session, name, testCodes, sortOrder);
        return subject;
    }

    public void Update(ExamSession session, string name, IEnumerable<string> testCodes, int sortOrder)
    {
        var codes = testCodes.ToList();
        if (codes.Count == 0)
        {
            throw new ArgumentException($"Subject {Id} must belong to at least one test.", nameof(testCodes));
        }

        Session = session;
        Name = name;
        if (!TestCodes.SequenceEqual(codes))
        {
            TestCodes = codes;
        }

        SortOrder = sortOrder;
    }
}
