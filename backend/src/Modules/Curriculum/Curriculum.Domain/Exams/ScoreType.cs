using Nirengi.BuildingBlocks.Domain.Entities;

namespace Nirengi.Modules.Curriculum.Domain.Exams;

/// <summary>A score type (SAY, EA, SOZ, DIL) and the tests whose topics a student of that type studies.</summary>
public sealed class ScoreType : Entity<string>
{
    public const int CodeMaxLength = 10;

    public const int NameMaxLength = 50;

    private ScoreType()
    {
    }

    public string Name { get; private set; } = null!;

    public List<string> TestCodes { get; private set; } = [];

    public int SortOrder { get; private set; }

    public static ScoreType Create(string code, string name, IEnumerable<string> testCodes, int sortOrder)
    {
        var scoreType = new ScoreType { Id = code };
        scoreType.Update(name, testCodes, sortOrder);
        return scoreType;
    }

    public void Update(string name, IEnumerable<string> testCodes, int sortOrder)
    {
        var codes = testCodes.ToList();
        Name = name;
        if (!TestCodes.SequenceEqual(codes))
        {
            TestCodes = codes;
        }

        SortOrder = sortOrder;
    }
}
