namespace Nirengi.Modules.Curriculum.Contracts;

/// <param name="IsActive">False when the topic was dropped from the curriculum; old records may still use it.</param>
public sealed record CatalogTopic(Guid Id, string SubjectCode, bool IsActive);

public sealed record CatalogSource(Guid Id, bool IsActive);

/// <summary>A line of the mock exam score sheet template.</summary>
/// <param name="Session">"TYT" or "AYT".</param>
/// <param name="SubjectCodes">Subjects whose topics a wrong answer in this section may be tagged with.</param>
/// <param name="ParentCode">The block this subject line belongs to; null for a root (one per test).</param>
/// <param name="AlternativeOf">The sibling this line replaces (philosophy instead of religion).</param>
public sealed record CatalogMockSection(
    string Code,
    string TestCode,
    string Session,
    IReadOnlyList<string> SubjectCodes,
    int QuestionCount,
    string? ParentCode,
    string? AlternativeOf);

/// <summary>Lets other modules check topic and source references without reading Curriculum's tables.</summary>
public interface ICurriculumCatalog
{
    /// <summary>The known topics among <paramref name="ids"/>; unknown ids are simply missing from the result.</summary>
    Task<IReadOnlyList<CatalogTopic>> GetTopics(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<CatalogSource?> GetSource(Guid id, CancellationToken cancellationToken);

    /// <summary>The whole mock exam template (a few dozen rows).</summary>
    Task<IReadOnlyList<CatalogMockSection>> GetMockSections(CancellationToken cancellationToken);
}
