using Microsoft.EntityFrameworkCore;
using Nirengi.Modules.Tracking.Contracts;
using Nirengi.Modules.Tracking.Domain.MockExams;

namespace Nirengi.Modules.Tracking.Infrastructure.Persistence;

internal sealed class TrackingActivityReader(TrackingDbContext db) : ITrackingActivityReader
{
    public async Task<IReadOnlyList<TopicActivity>> GetTopicActivity(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        await db.TopicTests.AsNoTracking()
            .Where(t => t.UserId == userId && t.DeletedAt == null && t.StudiedOn >= from && t.StudiedOn <= to)
            .SelectMany(t => t.Results.Select(r => new { t.StudiedOn, r.TopicId, r.Total }))
            .GroupBy(x => new { x.StudiedOn, x.TopicId })
            .Select(g => new TopicActivity(g.Key.StudiedOn, g.Key.TopicId, g.Sum(x => x.Total)))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PendingTagging>> GetPendingTagging(Guid userId, DateOnly from, CancellationToken cancellationToken)
    {
        var rows = await db.MockExams.AsNoTracking()
            .Where(e => e.UserId == userId && e.DeletedAt == null && e.TakenOn >= from)
            .SelectMany(e => e.Sections
                .Where(s => s.TaggingStatus != TaggingStatus.Full && s.Wrong + s.Blank > 0)
                .Select(s => new { e.Id, e.TakenOn, e.ClientModifiedAt, s.SectionCode, Missed = s.Wrong + s.Blank }))
            .ToListAsync(cancellationToken);
        return [.. rows
            .OrderByDescending(r => r.TakenOn)
            .ThenByDescending(r => r.ClientModifiedAt)
            .ThenBy(r => r.SectionCode, StringComparer.Ordinal)
            .Select(r => new PendingTagging(r.Id, r.TakenOn, r.SectionCode, r.Missed))];
    }

    public async Task<IReadOnlyList<DeclaredProgress>> GetProgress(Guid userId, CancellationToken cancellationToken) =>
        [.. (await db.TopicProgress.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(cancellationToken))
            .Select(p => new DeclaredProgress(p.TopicId, p.Status.ToString()))];
}
