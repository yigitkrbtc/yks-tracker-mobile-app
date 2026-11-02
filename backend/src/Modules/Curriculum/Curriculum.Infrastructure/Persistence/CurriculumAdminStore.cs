using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Domain.Results;
using Nirengi.Modules.Curriculum.Application.Abstractions;
using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Curriculum.Domain.Versions;
using Nirengi.Modules.Curriculum.Infrastructure.Seed;

namespace Nirengi.Modules.Curriculum.Infrastructure.Persistence;

internal sealed class CurriculumAdminStore(CurriculumDbContext db, CurriculumPublisher publisher) : ICurriculumAdminStore
{
    private static readonly JsonSerializerOptions SeedJson = new(CurriculumJson.Options) { WriteIndented = true };

    public Task<CurriculumRelease?> GetRelease(CancellationToken cancellationToken) =>
        db.Releases.AsNoTracking().OrderByDescending(r => r.ExamYear).FirstOrDefaultAsync(cancellationToken);

    public Task<CurriculumContent> LoadContent(CancellationToken cancellationToken) => publisher.LoadContent(cancellationToken);

    public async Task<IReadOnlyList<ExamTest>> GetTests(CancellationToken cancellationToken) =>
        await db.Tests.AsNoTracking().OrderBy(t => t.SortOrder).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Subject>> GetSubjects(CancellationToken cancellationToken) =>
        await db.Subjects.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TopicWeight>> GetWeights(int examYear, CancellationToken cancellationToken) =>
        await db.TopicWeights.AsNoTracking().Where(w => w.ExamYear == examYear).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CurriculumDraft>> GetDrafts(CancellationToken cancellationToken) =>
        await db.Drafts.AsNoTracking().OrderBy(d => d.ExamYear).ThenBy(d => d.Kind).ToListAsync(cancellationToken);

    public Task<CurriculumDraft?> GetDraft(Guid id, CancellationToken cancellationToken) =>
        db.Drafts.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<CurriculumDraft?> GetManualDraft(int examYear, CancellationToken cancellationToken) =>
        db.Drafts.SingleOrDefaultAsync(d => d.ExamYear == examYear && d.Kind == DraftKind.Manual, cancellationToken);

    public void AddDraft(CurriculumDraft draft) => db.Drafts.Add(draft);

    public void RemoveDraft(CurriculumDraft draft) => db.Drafts.Remove(draft);

    public async Task<IReadOnlyList<CurriculumVersion>> GetVersions(int examYear, CancellationToken cancellationToken) =>
        await db.Versions.AsNoTracking().Where(v => v.ExamYear == examYear).OrderByDescending(v => v.Number).ToListAsync(cancellationToken);

    public Task<CurriculumVersion?> GetVersion(int examYear, int number, CancellationToken cancellationToken) =>
        db.Versions.AsNoTracking().SingleOrDefaultAsync(v => v.ExamYear == examYear && v.Number == number, cancellationToken);

    public async Task<Result<CurriculumVersion>> Publish(Guid draftId, int expectedRevision, Guid? actor, string summary, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await publisher.Lock(cancellationToken);

        var draft = await db.Drafts.SingleOrDefaultAsync(d => d.Id == draftId, cancellationToken);
        if (draft is null)
        {
            return CurriculumErrors.DraftNotFound;
        }

        if (draft.Revision != expectedRevision)
        {
            return CurriculumErrors.DraftChanged;
        }

        var seed = draft.SeedHash is null ? null : CurriculumSeed.LoadEmbedded();
        var result = await publisher.PublishLocked(draft, actor, summary, seed, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<bool> TrySaveChanges(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Concurrency token mismatch, or a second manual draft for the year created meanwhile.
            return false;
        }
    }

    public async Task<byte[]> ExportSeed(CancellationToken cancellationToken)
    {
        var content = await publisher.LoadContent(cancellationToken);
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "konu_taksonomisi.csv", Csv(
                ["session", "unit", "topic_code", "topic", "subtopics", "grade", "description"],
                content.Topics.Where(t => t.IsActive).Select(t => new[]
                {
                    t.SubjectCode.Split('.')[0],
                    t.SubjectCode.Split('.')[1],
                    t.Code,
                    t.Name,
                    string.Join(';', t.Subtopics),
                    t.GradeMin == t.GradeMax
                        ? t.GradeMin.ToString(CultureInfo.InvariantCulture)
                        : string.Create(CultureInfo.InvariantCulture, $"{t.GradeMin}-{t.GradeMax}"),
                    t.Description ?? "",
                })));
            Write(zip, "kaynaklar.csv", Csv(
                ["source_code", "name", "kind"],
                content.Sources.Where(s => s.IsActive).Select(s => new[] { s.Code, s.Name, Kind(s.Kind) })));
            Write(zip, "exam_calendar.json", JsonSerializer.Serialize(new { calendars = content.Calendars }, SeedJson) + "\n");
            Write(zip, "BENIOKU.txt",
                "Panelde yayınlanan müfredattan üretilen seed dosyaları (ADR-0019).\n"
                + "data/seed altındaki aynı adlı dosyaların yerine koyun, sonra `python3 tools/aggregate.py --repo` ile\n"
                + "konu_agirliklari.csv'yi yeniden üretin (yeni konular q = 0 ile eklenir) ve PR açın.\n");
        }

        return buffer.ToArray();
    }

    private static void Write(ZipArchive zip, string name, string text)
    {
        using var stream = zip.CreateEntry(name, CompressionLevel.Optimal).Open();
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
        stream.Write(bytes);
    }

    private static string Csv(string[] header, IEnumerable<string[]> rows)
    {
        var text = new StringBuilder();
        text.Append(string.Join(',', header)).Append('\n');
        foreach (var row in rows)
        {
            text.Append(string.Join(',', row.Select(Field))).Append('\n');
        }

        return text.ToString();
    }

    private static string Field(string value) =>
        value.AsSpan().IndexOfAny(",\"\n\r") >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;

    private static string Kind(SourceKind kind) => kind switch
    {
        SourceKind.Official => "official",
        SourceKind.Institution => "institution",
        _ => "publisher",
    };
}
