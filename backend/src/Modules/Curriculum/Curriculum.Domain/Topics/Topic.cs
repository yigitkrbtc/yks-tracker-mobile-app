using Nirengi.BuildingBlocks.Domain.Entities;

namespace Nirengi.Modules.Curriculum.Domain.Topics;

public enum TopicStatus
{
    Active = 0,

    /// <summary>Dropped from the seed; kept because study records may still point at it.</summary>
    Retired = 1,
}

/// <summary>A topic students study and record results against; the code (e.g. AYT.AMAT.26) is its permanent key.</summary>
public sealed class Topic : Entity<Guid>
{
    public const int CodeMaxLength = 30;

    public const int NameMaxLength = 200;

    public const int DescriptionMaxLength = 500;

    private Topic()
    {
    }

    public string Code { get; private set; } = null!;

    public string SubjectCode { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public List<string> Subtopics { get; private set; } = [];

    /// <summary>Lowest and highest school grade (9–12) the topic is taught in.</summary>
    public int GradeMin { get; private set; }

    public int GradeMax { get; private set; }

    public string? Description { get; private set; }

    public TopicStatus Status { get; private set; }

    public int SortOrder { get; private set; }

    public static Topic Create(string code, string subjectCode, string name, IEnumerable<string> subtopics, int gradeMin, int gradeMax, string? description, int sortOrder)
    {
        var topic = new Topic { Id = TopicIdentity.For(code), Code = code };
        topic.Update(subjectCode, name, subtopics, gradeMin, gradeMax, description, sortOrder);
        return topic;
    }

    public void Update(string subjectCode, string name, IEnumerable<string> subtopics, int gradeMin, int gradeMax, string? description, int sortOrder)
    {
        if (gradeMin is < 9 or > 12 || gradeMax < gradeMin || gradeMax > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(gradeMin), $"Topic {Code} has an invalid grade range {gradeMin}-{gradeMax}.");
        }

        SubjectCode = subjectCode;
        Name = name;
        var list = subtopics.ToList();
        if (!Subtopics.SequenceEqual(list))
        {
            Subtopics = list;
        }

        GradeMin = gradeMin;
        GradeMax = gradeMax;
        Description = description;
        SortOrder = sortOrder;
        Status = TopicStatus.Active;
    }

    public void Retire() => Status = TopicStatus.Retired;
}
