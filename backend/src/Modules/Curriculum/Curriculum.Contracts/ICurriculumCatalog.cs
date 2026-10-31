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

/// <summary>A topic with its expected question count for the release's exam year (q_i).</summary>
public sealed record CatalogWeightedTopic(Guid Id, string SubjectCode, decimal ExpectedQuestions, bool IsActive);

public sealed record CatalogSubject(string Code, IReadOnlyList<string> TestCodes);

public sealed record CatalogScoreType(string Code, IReadOnlyList<string> TestCodes);

/// <summary>The curriculum slice the analysis engine needs.</summary>
/// <param name="Version">Changes whenever any topic, weight or mapping changes (seed hash).</param>
/// <param name="Topics">Retired topics included (old records use them), in display order.</param>
public sealed record AnalysisCatalogData(
    string Version,
    int ExamYear,
    IReadOnlyList<CatalogWeightedTopic> Topics,
    IReadOnlyList<CatalogSubject> Subjects,
    IReadOnlyList<CatalogScoreType> ScoreTypes,
    IReadOnlyList<CatalogMockSection> MockSections);

/// <summary>Lets other modules check topic and source references without reading Curriculum's tables.</summary>
public interface ICurriculumCatalog
{
    /// <summary>The known topics among <paramref name="ids"/>; unknown ids are simply missing from the result.</summary>
    Task<IReadOnlyList<CatalogTopic>> GetTopics(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<CatalogSource?> GetSource(Guid id, CancellationToken cancellationToken);

    /// <summary>The whole mock exam template (a few dozen rows).</summary>
    Task<IReadOnlyList<CatalogMockSection>> GetMockSections(CancellationToken cancellationToken);

    /// <summary>The latest release's analysis catalog; null before the seed is loaded.</summary>
    Task<AnalysisCatalogData?> GetAnalysisCatalog(CancellationToken cancellationToken);

    /// <summary>The latest release's version (seed hash), cheap to read; null before the seed is loaded.</summary>
    Task<string?> GetVersion(CancellationToken cancellationToken);
}
