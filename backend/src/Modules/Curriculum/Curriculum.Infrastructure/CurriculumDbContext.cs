using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Calendar;
using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Sources;
using Nirengi.Modules.Curriculum.Domain.Topics;
using Nirengi.Modules.Curriculum.Domain.Versions;

namespace Nirengi.Modules.Curriculum.Infrastructure;

public sealed class CurriculumDbContext(DbContextOptions<CurriculumDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "curriculum";

    public DbSet<ExamTest> Tests => Set<ExamTest>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<ScoreType> ScoreTypes => Set<ScoreType>();

    public DbSet<MockSection> MockSections => Set<MockSection>();

    public DbSet<Topic> Topics => Set<Topic>();

    public DbSet<TopicWeight> TopicWeights => Set<TopicWeight>();

    public DbSet<Source> Sources => Set<Source>();

    public DbSet<CurriculumRelease> Releases => Set<CurriculumRelease>();

    public DbSet<CurriculumVersion> Versions => Set<CurriculumVersion>();

    public DbSet<CurriculumDraft> Drafts => Set<CurriculumDraft>();

    public DbSet<TopicLineage> Lineage => Set<TopicLineage>();

    public DbSet<StoredExamCalendar> Calendars => Set<StoredExamCalendar>();

    protected override string Schema => SchemaName;
}
