using Microsoft.EntityFrameworkCore;
using Nirengi.Modules.Curriculum.Contracts;
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
        var subjects = await db.Subjects.AsNoTracking().ToListAsync(cancellationToken);
        return [.. sections.Select(s => new CatalogMockSection(
            s.Id,
            s.TestCode,
            tests[s.TestCode].ToString(),
            s.SubjectCode is { } code ? [code] : [.. subjects.Where(x => x.TestCodes.Contains(s.TestCode)).Select(x => x.Id)],
            s.QuestionCount,
            s.ParentCode,
            s.AlternativeOf))];
    }
}
