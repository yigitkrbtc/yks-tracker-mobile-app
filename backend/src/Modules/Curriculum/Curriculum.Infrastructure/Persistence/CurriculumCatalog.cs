using Microsoft.EntityFrameworkCore;
using Nirengi.Modules.Curriculum.Contracts;
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
}
