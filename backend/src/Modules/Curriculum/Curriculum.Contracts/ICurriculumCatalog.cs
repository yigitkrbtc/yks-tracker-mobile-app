namespace Nirengi.Modules.Curriculum.Contracts;

/// <param name="IsActive">False when the topic was dropped from the curriculum; old records may still use it.</param>
public sealed record CatalogTopic(Guid Id, string SubjectCode, bool IsActive);

public sealed record CatalogSource(Guid Id, bool IsActive);

/// <summary>Lets other modules check topic and source references without reading Curriculum's tables.</summary>
public interface ICurriculumCatalog
{
    /// <summary>The known topics among <paramref name="ids"/>; unknown ids are simply missing from the result.</summary>
    Task<IReadOnlyList<CatalogTopic>> GetTopics(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<CatalogSource?> GetSource(Guid id, CancellationToken cancellationToken);
}
