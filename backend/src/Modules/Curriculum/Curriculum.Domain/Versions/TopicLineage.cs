namespace Nirengi.Modules.Curriculum.Domain.Versions;

/// <summary>A merge or split between two topics, recorded when the version that made it is published.</summary>
public sealed class TopicLineage
{
    private TopicLineage()
    {
    }

    public Guid FromTopicId { get; private set; }

    public Guid ToTopicId { get; private set; }

    public LineageRelation Relation { get; private set; }

    public int ExamYear { get; private set; }

    public int VersionNumber { get; private set; }

    public static TopicLineage Create(Guid fromTopicId, Guid toTopicId, LineageRelation relation, int examYear, int versionNumber) =>
        new() { FromTopicId = fromTopicId, ToTopicId = toTopicId, Relation = relation, ExamYear = examYear, VersionNumber = versionNumber };
}
