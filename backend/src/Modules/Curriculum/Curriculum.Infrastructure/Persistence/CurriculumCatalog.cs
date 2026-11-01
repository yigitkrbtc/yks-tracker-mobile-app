using Microsoft.EntityFrameworkCore;
using Nirengi.Modules.Curriculum.Contracts;
using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;

namespace Nirengi.Modules.Curriculum.Infrastructure.Persistence;

internal sealed class CurriculumCatalog(CurriculumDbContext db) : ICurriculumCatalog
{
    public async Task<IReadOnlyList<CatalogTopic>> GetTopics(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.Topics.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new CatalogTopic(t.Id, t.SubjectCode, t.Status == TopicStatus.Active))
            .ToListAsync(cancellationToken);

    public Task<CatalogSource?> GetSource(Guid id, CancellationToken cancellationToken) =>
        db.Sources.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new CatalogSource(s.Id, s.Status == SourceStatus.Active))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CatalogMockSection>> GetMockSections(CancellationToken cancellationToken)
    {
        var sections = await db.MockSections.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);
        var tests = await db.Tests.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Session, cancellationToken);
        var subjects = await db.Subjects.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);
        return [.. sections.Select(s => new CatalogMockSection(
            s.Id,
            s.TestCode,
            tests[s.TestCode].ToString(),
            s.SubjectCode is { } code ? [code] : [.. subjects.Where(x => x.TestCodes.Contains(s.TestCode)).Select(x => x.Id)],
            s.QuestionCount,
            s.ParentCode,
            s.AlternativeOf))];
    }

    public async Task<AnalysisCatalogData?> GetAnalysisCatalog(CancellationToken cancellationToken)
    {
        var release = await LatestRelease(cancellationToken);
        if (release is null)
        {
            return null;
        }

        var subjects = await db.Subjects.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);
        var subjectOrder = subjects.Select((s, i) => (s.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var weights = await db.TopicWeights.AsNoTracking()
            .Where(w => w.ExamYear == release.ExamYear)
            .ToDictionaryAsync(w => w.TopicId, w => w.ExpectedQuestions, cancellationToken);
        var topics = (await db.Topics.AsNoTracking().ToListAsync(cancellationToken))
            .OrderBy(t => subjectOrder.GetValueOrDefault(t.SubjectCode, int.MaxValue))
            .ThenBy(t => t.SortOrder)
            .ThenBy(t => t.Code, StringComparer.Ordinal)
            .Select(t => new CatalogWeightedTopic(t.Id, t.SubjectCode, weights.GetValueOrDefault(t.Id), t.Status == TopicStatus.Active))
            .ToList();
        var scoreTypes = await db.ScoreTypes.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);

        return new AnalysisCatalogData(
            Version(release),
            release.ExamYear,
            topics,
            [.. subjects.Select(s => new CatalogSubject(s.Id, [.. s.TestCodes]))],
            [.. scoreTypes.Select(s => new CatalogScoreType(s.Id, [.. s.TestCodes]))],
            await GetMockSections(cancellationToken));
    }

    public async Task<string?> GetVersion(CancellationToken cancellationToken) =>
        await LatestRelease(cancellationToken) is { } release ? Version(release) : null;

    private Task<CurriculumRelease?> LatestRelease(CancellationToken cancellationToken) =>
        db.Releases.AsNoTracking().OrderByDescending(r => r.ExamYear).FirstOrDefaultAsync(cancellationToken);

    private static string Version(CurriculumRelease release) => $"{release.ExamYear}:{release.SeedHash}";
}
