using Nirengi.BuildingBlocks.Domain.Entities;

namespace Nirengi.Modules.Curriculum.Domain.Exams;

/// <summary>
/// One line of a mock exam's score sheet (e.g. TYT Sosyal → Tarih 5). Each test has one root section; a block
/// test (Sosyal, Fen, AYT TDE-SB1 and SB2) also has a child per subject, and a student enters either the block
/// total or its children. An alternative child (philosophy instead of religion) replaces its sibling.
/// </summary>
public sealed class MockSection : Entity<string>
{
    public const int CodeMaxLength = 20;

    public const int NameMaxLength = 100;

    private MockSection()
    {
    }

    public string TestCode { get; private set; } = null!;

    /// <summary>Null when the section spans every subject of its test (a block, or Matematik with geometry).</summary>
    public string? SubjectCode { get; private set; }

    public string Name { get; private set; } = null!;

    public int QuestionCount { get; private set; }

    public string? ParentCode { get; private set; }

    public string? AlternativeOf { get; private set; }

    public int SortOrder { get; private set; }

    public static MockSection Create(
        string code,
        string testCode,
        string? subjectCode,
        string name,
        int questionCount,
        string? parentCode,
        string? alternativeOf,
        int sortOrder)
    {
        var section = new MockSection { Id = code };
        section.Update(testCode, subjectCode, name, questionCount, parentCode, alternativeOf, sortOrder);
        return section;
    }

    public void Update(
        string testCode,
        string? subjectCode,
        string name,
        int questionCount,
        string? parentCode,
        string? alternativeOf,
        int sortOrder)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(questionCount);
        TestCode = testCode;
        SubjectCode = subjectCode;
        Name = name;
        QuestionCount = questionCount;
        ParentCode = parentCode;
        AlternativeOf = alternativeOf;
        SortOrder = sortOrder;
    }
}
