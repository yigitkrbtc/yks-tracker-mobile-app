using Microsoft.EntityFrameworkCore;
using Nirengi.Modules.Curriculum.Application.Abstractions;
using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Topics;

namespace Nirengi.Modules.Curriculum.Infrastructure.Persistence;

internal sealed class CurriculumRepository(CurriculumDbContext db) : ICurriculumRepository
{
    public Task<CurriculumRelease?> GetLatestRelease(CancellationToken cancellationToken) =>
        db.Releases.AsNoTracking().OrderByDescending(r => r.ExamYear).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ScoreType>> GetScoreTypes(CancellationToken cancellationToken) =>
        await db.ScoreTypes.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExamTest>> GetTests(CancellationToken cancellationToken) =>
        await db.Tests.AsNoTracking().OrderBy(t => t.SortOrder).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Subject>> GetSubjects(CancellationToken cancellationToken) =>
        await db.Subjects.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Topic>> GetActiveTopics(CancellationToken cancellationToken) =>
        await db.Topics.AsNoTracking().Where(t => t.Status == TopicStatus.Active).OrderBy(t => t.SortOrder).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TopicWeight>> GetWeights(int examYear, CancellationToken cancellationToken) =>
        await db.TopicWeights.AsNoTracking().Where(w => w.ExamYear == examYear).ToListAsync(cancellationToken);
}
