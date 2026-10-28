using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Topics;

namespace Nirengi.Modules.Curriculum.Infrastructure.Seed;

/// <summary>
/// Brings the curriculum tables in line with data/seed. Runs right after migrations; a no-op when the seed hash
/// is already loaded. Topics are matched by code and never deleted (study records point at them): a topic
/// missing from the seed is retired, one that comes back is reactivated.
/// </summary>
public sealed partial class CurriculumSeeder(CurriculumDbContext db, IClock clock, ILogger<CurriculumSeeder> logger)
{
    /// <returns>True when anything was written.</returns>
    public Task<bool> Seed(CancellationToken cancellationToken) => Apply(CurriculumSeed.LoadEmbedded(), cancellationToken);

    internal async Task<bool> Apply(CurriculumSeed seed, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Several API instances may start together; only one imports, the rest then see the hash and skip.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('curriculum.seed'))", cancellationToken);

        var release = await db.Releases.SingleOrDefaultAsync(r => r.ExamYear == seed.ExamYear, cancellationToken);
        if (release?.SeedHash == seed.Hash)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        await SyncTests(seed, cancellationToken);
        await SyncSubjects(seed, cancellationToken);
        await SyncScoreTypes(seed, cancellationToken);
        var topicIds = await SyncTopics(seed, cancellationToken);
        await SyncWeights(seed, topicIds, cancellationToken);

        var now = clock.UtcNow;
        if (release is null)
        {
            db.Releases.Add(CurriculumRelease.Create(seed.ExamYear, seed.Hash, now));
        }
        else
        {
            release.Reseed(seed.Hash, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogSeeded(logger, seed.ExamYear, seed.Topics.Count, seed.Hash);
        return true;
    }

    private async Task SyncTests(CurriculumSeed seed, CancellationToken cancellationToken)
    {
        var existing = await db.Tests.ToDictionaryAsync(t => t.Id, cancellationToken);
        for (var i = 0; i < seed.Tests.Count; i++)
        {
            var s = seed.Tests[i];
            if (existing.Remove(s.Code, out var test))
            {
                test.Update(s.Session, s.Name, s.QuestionCount, i);
            }
            else
            {
                db.Tests.Add(ExamTest.Create(s.Code, s.Session, s.Name, s.QuestionCount, i));
            }
        }

        db.Tests.RemoveRange(existing.Values);
    }

    private async Task SyncSubjects(CurriculumSeed seed, CancellationToken cancellationToken)
    {
        var existing = await db.Subjects.ToDictionaryAsync(s => s.Id, cancellationToken);
        for (var i = 0; i < seed.Subjects.Count; i++)
        {
            var s = seed.Subjects[i];
            if (existing.Remove(s.Code, out var subject))
            {
                subject.Update(s.Session, s.Name, s.TestCodes, i);
            }
            else
            {
                db.Subjects.Add(Subject.Create(s.Code, s.Session, s.Name, s.TestCodes, i));
            }
        }

        db.Subjects.RemoveRange(existing.Values);
    }

    private async Task SyncScoreTypes(CurriculumSeed seed, CancellationToken cancellationToken)
    {
        var existing = await db.ScoreTypes.ToDictionaryAsync(s => s.Id, cancellationToken);
        for (var i = 0; i < seed.ScoreTypes.Count; i++)
        {
            var s = seed.ScoreTypes[i];
            if (existing.Remove(s.Code, out var scoreType))
            {
                scoreType.Update(s.Name, s.TestCodes, i);
            }
            else
            {
                db.ScoreTypes.Add(ScoreType.Create(s.Code, s.Name, s.TestCodes, i));
            }
        }

        db.ScoreTypes.RemoveRange(existing.Values);
    }

    private async Task<Dictionary<string, Guid>> SyncTopics(CurriculumSeed seed, CancellationToken cancellationToken)
    {
        var existing = await db.Topics.ToDictionaryAsync(t => t.Code, cancellationToken);
        var ids = new Dictionary<string, Guid>(seed.Topics.Count, StringComparer.Ordinal);
        for (var i = 0; i < seed.Topics.Count; i++)
        {
            var s = seed.Topics[i];
            if (existing.Remove(s.Code, out var topic))
            {
                topic.Update(s.SubjectCode, s.Name, s.Subtopics, s.GradeMin, s.GradeMax, s.Description, i);
            }
            else
            {
                topic = Topic.Create(s.Code, s.SubjectCode, s.Name, s.Subtopics, s.GradeMin, s.GradeMax, s.Description, i);
                db.Topics.Add(topic);
            }

            ids[s.Code] = topic.Id;
        }

        foreach (var dropped in existing.Values)
        {
            dropped.Retire();
        }

        return ids;
    }

    private async Task SyncWeights(CurriculumSeed seed, Dictionary<string, Guid> topicIds, CancellationToken cancellationToken)
    {
        var existing = await db.TopicWeights.Where(w => w.ExamYear == seed.ExamYear).ToDictionaryAsync(w => w.TopicId, cancellationToken);
        foreach (var s in seed.Topics)
        {
            var topicId = topicIds[s.Code];
            if (existing.Remove(topicId, out var weight))
            {
                weight.Update(s.ExpectedQuestions, s.Tier, s.YearsAsked, s.YearsCounted);
            }
            else
            {
                db.TopicWeights.Add(TopicWeight.Create(topicId, seed.ExamYear, s.ExpectedQuestions, s.Tier, s.YearsAsked, s.YearsCounted));
            }
        }

        db.TopicWeights.RemoveRange(existing.Values);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Curriculum {ExamYear} seeded: {TopicCount} topics, hash {Hash}")]
    private static partial void LogSeeded(ILogger logger, int examYear, int topicCount, string hash);
}
