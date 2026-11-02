using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Application.Abstractions;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Calendar;
using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Curriculum.Domain.Versions;
using Nirengi.Modules.Curriculum.Infrastructure.Seed;

namespace Nirengi.Modules.Curriculum.Infrastructure.Persistence;

/// <summary>
/// Turns a draft into a published version (ADR-0019). Callers hold <see cref="Lock"/> inside a transaction; the seed
/// import at startup and the admin endpoint share this one path. Topics are matched by code and never deleted.
/// </summary>
public sealed class CurriculumPublisher(CurriculumDbContext db, IClock clock)
{
    /// <summary>Serialises publishers across API instances (same key as the old seed import).</summary>
    internal Task Lock(CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtext('curriculum.seed'))", cancellationToken);

    internal async Task<CurriculumContent> LoadContent(CancellationToken cancellationToken)
    {
        var topics = await db.Topics.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.Code).ToListAsync(cancellationToken);
        var sources = await db.Sources.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Code).ToListAsync(cancellationToken);
        var calendars = await db.Calendars.AsNoTracking().OrderBy(c => c.ExamYear).ToListAsync(cancellationToken);
        return new CurriculumContent(
            [.. topics.Select(t => new TopicState(
                t.Code, t.SubjectCode, t.Name, t.Subtopics, t.GradeMin, t.GradeMax, t.Description, t.SortOrder, t.Status == TopicStatus.Active))],
            [.. sources.Select(s => new SourceState(s.Code, s.Name, s.Kind, s.SortOrder, s.Status == SourceStatus.Active))],
            [.. calendars.Select(c => CurriculumJson.Deserialize<ExamCalendar>(c.Data))]);
    }

    /// <param name="seed">The embedded seed; required when the draft is a seed import (structure and weights come from it).</param>
    internal async Task<Result<CurriculumVersion>> PublishLocked(
        CurriculumDraft draft,
        Guid? actor,
        string summary,
        CurriculumSeed? seed,
        CancellationToken cancellationToken)
    {
        var release = await db.Releases.SingleOrDefaultAsync(r => r.ExamYear == draft.ExamYear, cancellationToken);
        var current = release?.VersionNumber ?? 0;
        if (draft.BasedOnNumber != current)
        {
            return CurriculumErrors.DraftStale;
        }

        if (draft.SeedHash is not null && (seed is null || seed.Hash != draft.SeedHash || seed.ExamYear != draft.ExamYear))
        {
            return CurriculumErrors.SeedChanged;
        }

        if (release is null && seed is null)
        {
            return CurriculumErrors.NotSeeded;
        }

        var subjectCodes = seed is not null
            ? seed.Subjects.Select(s => s.Code).ToHashSet(StringComparer.Ordinal)
            : (await db.Subjects.AsNoTracking().Select(s => s.Id).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var live = await LoadContent(cancellationToken);
        var applied = DraftApplier.Apply(live, subjectCodes, live.Topics.Select(t => t.Code).ToHashSet(StringComparer.Ordinal), CurriculumJson.Operations(draft.Operations));
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        var now = clock.UtcNow;
        if (seed is not null)
        {
            await SyncTests(seed, cancellationToken);
            await SyncSubjects(seed, cancellationToken);
            await SyncScoreTypes(seed, cancellationToken);
            await SyncMockSections(seed, cancellationToken);
        }

        await WriteContent(applied.Value.Content, cancellationToken);
        if (seed is not null)
        {
            await SyncWeights(seed, cancellationToken);
        }

        var number = current + 1;
        foreach (var link in applied.Value.Lineage)
        {
            var from = TopicIdentity.For(link.FromCode);
            var to = TopicIdentity.For(link.ToCode);
            if (!await db.Lineage.AnyAsync(l => l.FromTopicId == from && l.ToTopicId == to, cancellationToken))
            {
                db.Lineage.Add(TopicLineage.Create(from, to, link.Relation, draft.ExamYear, number));
            }
        }

        var seedHash = seed?.Hash ?? release!.SeedHash;
        var snapshot = CurriculumJson.Serialize(applied.Value.Content);
        var contentHash = CurriculumJson.ContentHash(seedHash, snapshot);
        if (release is null)
        {
            release = CurriculumRelease.Create(draft.ExamYear, seedHash, now);
            db.Releases.Add(release);
        }
        else if (seed is not null)
        {
            release.Reseed(seed.Hash, now);
        }

        release.Publish(number, contentHash);
        var version = CurriculumVersion.Create(
            draft.ExamYear,
            number,
            now,
            actor,
            summary,
            contentHash,
            seed?.Hash,
            snapshot,
            CurriculumJson.Serialize(applied.Value.Operations));
        db.Versions.Add(version);
        if (db.Entry(draft).State != EntityState.Detached)
        {
            db.Drafts.Remove(draft);
        }

        await db.SaveChangesAsync(cancellationToken);
        return version;
    }

    /// <summary>A database seeded before versions existed gets version 1 = what it holds now.</summary>
    internal async Task BackfillFirstVersion(CurriculumRelease release, CancellationToken cancellationToken)
    {
        if (await db.Versions.AnyAsync(v => v.ExamYear == release.ExamYear, cancellationToken))
        {
            return;
        }

        var snapshot = CurriculumJson.Serialize(await LoadContent(cancellationToken));
        var hash = CurriculumJson.ContentHash(release.SeedHash, snapshot);
        db.Versions.Add(CurriculumVersion.Create(release.ExamYear, 1, clock.UtcNow, null, "İlk sürüm", hash, release.SeedHash, snapshot, "[]"));
        release.Publish(1, hash);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task WriteContent(CurriculumContent content, CancellationToken cancellationToken)
    {
        var topics = await db.Topics.ToDictionaryAsync(t => t.Code, cancellationToken);
        foreach (var t in content.Topics)
        {
            if (topics.TryGetValue(t.Code, out var topic))
            {
                topic.Update(t.SubjectCode, t.Name, t.Subtopics, t.GradeMin, t.GradeMax, t.Description, t.SortOrder);
            }
            else
            {
                topic = Topic.Create(t.Code, t.SubjectCode, t.Name, t.Subtopics, t.GradeMin, t.GradeMax, t.Description, t.SortOrder);
                db.Topics.Add(topic);
            }

            if (!t.IsActive)
            {
                topic.Retire();
            }
        }

        var sources = await db.Sources.ToDictionaryAsync(s => s.Code, cancellationToken);
        foreach (var s in content.Sources)
        {
            if (sources.TryGetValue(s.Code, out var source))
            {
                source.Update(s.Name, s.Kind, s.SortOrder);
            }
            else
            {
                source = Source.Create(s.Code, s.Name, s.Kind, s.SortOrder);
                db.Sources.Add(source);
            }

            if (!s.IsActive)
            {
                source.Retire();
            }
        }

        var calendars = await db.Calendars.ToDictionaryAsync(c => c.ExamYear, cancellationToken);
        foreach (var c in content.Calendars)
        {
            var data = CurriculumJson.Serialize(c);
            if (calendars.TryGetValue(c.ExamYear, out var stored))
            {
                if (stored.Data != data)
                {
                    stored.Replace(data);
                }
            }
            else
            {
                db.Calendars.Add(StoredExamCalendar.Create(c.ExamYear, data));
            }
        }
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

    /// <summary>Removed codes are deleted: mock exams keep their own copy of a section's question count.</summary>
    private async Task SyncMockSections(CurriculumSeed seed, CancellationToken cancellationToken)
    {
        var existing = await db.MockSections.ToDictionaryAsync(s => s.Id, cancellationToken);
        for (var i = 0; i < seed.MockSections.Count; i++)
        {
            var s = seed.MockSections[i];
            if (existing.Remove(s.Code, out var section))
            {
                section.Update(s.TestCode, s.SubjectCode, s.Name, s.QuestionCount, s.ParentCode, s.AlternativeOf, i);
            }
            else
            {
                db.MockSections.Add(MockSection.Create(s.Code, s.TestCode, s.SubjectCode, s.Name, s.QuestionCount, s.ParentCode, s.AlternativeOf, i));
            }
        }

        db.MockSections.RemoveRange(existing.Values);
    }

    /// <summary>Weights of topics the seed knows; a topic added in the admin panel has none until the seed catches up (q = 0).</summary>
    private async Task SyncWeights(CurriculumSeed seed, CancellationToken cancellationToken)
    {
        var existing = await db.TopicWeights.Where(w => w.ExamYear == seed.ExamYear)
            .ToDictionaryAsync(w => (w.TopicId, w.TestCode), cancellationToken);
        foreach (var s in seed.Topics)
        {
            var topicId = TopicIdentity.For(s.Code);
            foreach (var w in s.Weights)
            {
                if (existing.Remove((topicId, w.TestCode), out var weight))
                {
                    weight.Update(w.ExpectedQuestions, w.Tier, w.YearsAsked, w.YearsCounted);
                }
                else
                {
                    db.TopicWeights.Add(TopicWeight.Create(topicId, seed.ExamYear, w.TestCode, w.ExpectedQuestions, w.Tier, w.YearsAsked, w.YearsCounted));
                }
            }
        }

        db.TopicWeights.RemoveRange(existing.Values);
    }
}
