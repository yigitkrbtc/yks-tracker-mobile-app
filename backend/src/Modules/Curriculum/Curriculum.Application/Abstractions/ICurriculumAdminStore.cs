using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Curriculum.Domain.Versions;

namespace Nirengi.Modules.Curriculum.Application.Abstractions;

/// <summary>Write side of the admin panel (ADR-0019): drafts, versions and publishing.</summary>
public interface ICurriculumAdminStore
{
    Task<CurriculumRelease?> GetRelease(CancellationToken cancellationToken);

    /// <summary>Topics (retired included), sources and calendars as they are now.</summary>
    Task<CurriculumContent> LoadContent(CancellationToken cancellationToken);

    Task<IReadOnlyList<ExamTest>> GetTests(CancellationToken cancellationToken);

    Task<IReadOnlyList<Subject>> GetSubjects(CancellationToken cancellationToken);

    Task<IReadOnlyList<TopicWeight>> GetWeights(int examYear, CancellationToken cancellationToken);

    Task<IReadOnlyList<CurriculumDraft>> GetDrafts(CancellationToken cancellationToken);

    /// <summary>Tracked: changes are written by <see cref="SaveChanges"/>.</summary>
    Task<CurriculumDraft?> GetDraft(Guid id, CancellationToken cancellationToken);

    Task<CurriculumDraft?> GetManualDraft(int examYear, CancellationToken cancellationToken);

    void AddDraft(CurriculumDraft draft);

    void RemoveDraft(CurriculumDraft draft);

    Task<IReadOnlyList<CurriculumVersion>> GetVersions(int examYear, CancellationToken cancellationToken);

    Task<CurriculumVersion?> GetVersion(int examYear, int number, CancellationToken cancellationToken);

    /// <summary>Applies the draft in one transaction and deletes it. <paramref name="expectedRevision"/> guards against a lost update.</summary>
    Task<Result<CurriculumVersion>> Publish(Guid draftId, int expectedRevision, Guid? actor, string summary, CancellationToken cancellationToken);

    /// <summary>False when someone else changed a draft meanwhile (concurrency token).</summary>
    Task<bool> TrySaveChanges(CancellationToken cancellationToken);

    /// <summary>Seed files (taxonomy, sources, calendar) built from the live content, as a ZIP.</summary>
    Task<byte[]> ExportSeed(CancellationToken cancellationToken);
}
