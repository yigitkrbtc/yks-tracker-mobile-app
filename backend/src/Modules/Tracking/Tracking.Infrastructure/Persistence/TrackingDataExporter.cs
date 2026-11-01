using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Application.Export;
using Nirengi.Modules.Curriculum.Contracts;

namespace Nirengi.Modules.Tracking.Infrastructure.Persistence;

/// <summary>The student's records in Tracking (ADR-0018). Deleted records are left out; topic names come from Curriculum.</summary>
internal sealed class TrackingDataExporter(TrackingDbContext db, ICurriculumCatalog curriculum) : IPersonalDataExporter
{
    public int Order => 20;

    public async Task<IReadOnlyList<ExportFile>> Export(Guid userId, CancellationToken cancellationToken)
    {
        var catalog = await curriculum.GetAnalysisCatalog(cancellationToken);
        var topics = catalog?.Topics.ToDictionary(t => t.Id) ?? [];
        string? Code(Guid id) => topics.TryGetValue(id, out var t) ? t.Code : null;
        string? Name(Guid id) => topics.TryGetValue(id, out var t) ? t.Name : null;

        var files = new List<ExportFile>();

        var profile = await db.StudyProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == userId, cancellationToken);
        var profileCsv = new CsvTable("sinif", "puan_turu", "ikinci_puan_turu", "ydt_dili", "hedef_sinav_yili", "haftalik_soru_hedefi", "haftalik_aktif_gun_hedefi", "guncelleme");
        if (profile is not null)
        {
            profileCsv.Add(profile.Grade, profile.PrimaryScoreType, profile.SecondaryScoreType, profile.YdtLanguage, profile.TargetExamYear, profile.WeeklyQuestionGoal, profile.WeeklyActiveDaysGoal, profile.UpdatedAt);
        }

        files.Add(profileCsv.ToFile("calisma_profili.csv"));

        var tests = await db.TopicTests.AsNoTracking()
            .Include(t => t.Results)
            .Where(t => t.UserId == userId && t.DeletedAt == null)
            .OrderBy(t => t.StudiedOn).ThenBy(t => t.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var testCsv = new CsvTable("test_id", "tarih", "kaynak_id", "kaynak_adi", "tekrar", "zorluk", "sure_dk", "not", "olusturma");
        var rowCsv = new CsvTable("test_id", "tarih", "konu_kodu", "konu", "soru", "dogru", "yanlis", "bos", "net", "neden_bilgi", "neden_dikkat", "neden_sure");
        foreach (var t in tests)
        {
            testCsv.Add(t.Id, t.StudiedOn, t.SourceId, t.SourceName, t.IsRepeat, t.Difficulty, t.DurationMinutes, t.Note, t.CreatedAt);
            foreach (var r in t.Results.OrderBy(r => Code(r.TopicId), StringComparer.Ordinal))
            {
                rowCsv.Add(t.Id, t.StudiedOn, Code(r.TopicId), Name(r.TopicId), r.Total, r.Correct, r.Wrong, r.Blank, r.Net, r.MistakeKnowledge, r.MistakeAttention, r.MistakeTime);
            }
        }

        files.Add(testCsv.ToFile("konu_testleri.csv"));
        files.Add(rowCsv.ToFile("konu_testi_satirlari.csv"));

        var exams = await db.MockExams.AsNoTracking()
            .Include(e => e.Sections)
            .Include(e => e.Marks)
            .Where(e => e.UserId == userId && e.DeletedAt == null)
            .OrderBy(e => e.TakenOn).ThenBy(e => e.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var examCsv = new CsvTable("deneme_id", "tarih", "tur", "kaynak_id", "kaynak_adi", "ad", "sure_dk", "not", "olusturma");
        var sectionCsv = new CsvTable("deneme_id", "tarih", "bolum", "soru", "dogru", "yanlis", "bos", "net", "etiketleme");
        var markCsv = new CsvTable("deneme_id", "tarih", "bolum", "konu_kodu", "konu", "yanlis", "bos", "neden_bilgi", "neden_dikkat", "neden_sure");
        foreach (var e in exams)
        {
            examCsv.Add(e.Id, e.TakenOn, e.Kind, e.SourceId, e.SourceName, e.Title, e.DurationMinutes, e.Note, e.CreatedAt);
            foreach (var s in e.Sections.OrderBy(s => s.SectionCode, StringComparer.Ordinal))
            {
                sectionCsv.Add(e.Id, e.TakenOn, s.SectionCode, s.QuestionCount, s.Correct, s.Wrong, s.Blank, s.Net, s.TaggingStatus);
            }

            foreach (var m in e.Marks.OrderBy(m => m.SectionCode, StringComparer.Ordinal).ThenBy(m => Code(m.TopicId), StringComparer.Ordinal))
            {
                markCsv.Add(e.Id, e.TakenOn, m.SectionCode, Code(m.TopicId), Name(m.TopicId), m.Wrong, m.Blank, m.MistakeKnowledge, m.MistakeAttention, m.MistakeTime);
            }
        }

        files.Add(examCsv.ToFile("denemeler.csv"));
        files.Add(sectionCsv.ToFile("deneme_bolumleri.csv"));
        files.Add(markCsv.ToFile("deneme_isaretleri.csv"));

        var marks = await db.TopicProgress.AsNoTracking().Where(p => p.UserId == userId).ToListAsync(cancellationToken);
        var progressCsv = new CsvTable("konu_kodu", "konu", "durum", "guncelleme");
        foreach (var p in marks.OrderBy(p => Code(p.TopicId), StringComparer.Ordinal))
        {
            progressCsv.Add(Code(p.TopicId), Name(p.TopicId), p.Status, p.ClientModifiedAt);
        }

        files.Add(progressCsv.ToFile("konu_ilerlemesi.csv"));
        return files;
    }
}
