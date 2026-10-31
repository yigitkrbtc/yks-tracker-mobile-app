using Microsoft.EntityFrameworkCore;
using Nirengi.Modules.Tracking.Contracts;
using Nirengi.Modules.Tracking.Domain.MockExams;

namespace Nirengi.Modules.Tracking.Infrastructure.Persistence;

internal sealed class TrackingAnalyticsReader(TrackingDbContext db) : ITrackingAnalyticsReader
{
    // Tombstones keep their change_seq, so a delete moves the stamp too.
    public async Task<TrackingStamp> GetStamp(Guid userId, CancellationToken cancellationToken)
    {
        var tests = await db.TopicTests.Where(t => t.UserId == userId).MaxAsync(t => (long?)t.ChangeSeq, cancellationToken) ?? 0;
        var exams = await db.MockExams.Where(e => e.UserId == userId).MaxAsync(e => (long?)e.ChangeSeq, cancellationToken) ?? 0;
        var profile = await db.StudyProfiles.Where(p => p.Id == userId).Select(p => (DateTimeOffset?)p.UpdatedAt).SingleOrDefaultAsync(cancellationToken);
        return new TrackingStamp(Math.Max(tests, exams), profile);
    }

    public async Task<StudentHistory> GetHistory(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await db.StudyProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == userId, cancellationToken);
        var results = await db.TopicTests.AsNoTracking()
            .Where(t => t.UserId == userId && t.DeletedAt == null)
            .SelectMany(t => t.Results.Select(r => new HistoryTopicResult(
                t.Id, r.TopicId, t.StudiedOn, r.Total, r.Correct, r.Wrong, r.Blank, t.IsRepeat)))
            .ToListAsync(cancellationToken);
        var exams = await db.MockExams.AsNoTracking()
            .Include(e => e.Sections)
            .Include(e => e.Marks)
            .Where(e => e.UserId == userId && e.DeletedAt == null)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        string[] scoreTypes = profile is null
            ? []
            : profile.SecondaryScoreType is { } second
                ? [profile.PrimaryScoreType.ToString(), second.ToString()]
                : [profile.PrimaryScoreType.ToString()];

        return new StudentHistory(
            scoreTypes,
            results,
            [.. exams.Select(e => new HistoryMockExam(
                e.Id,
                e.TakenOn,
                [.. e.Sections.Select(s => new HistoryMockSection(
                    s.SectionCode,
                    s.QuestionCount,
                    s.Correct,
                    s.Wrong,
                    s.TaggingStatus == TaggingStatus.Full,
                    [.. e.Marks.Where(m => m.SectionCode == s.SectionCode).Select(m => new HistoryTopicMark(m.TopicId, m.Wrong, m.Blank))]))]))]);
    }
}
