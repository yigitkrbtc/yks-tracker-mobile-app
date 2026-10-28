using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Nirengi.Modules.Curriculum.Domain;
using Nirengi.Modules.Curriculum.Domain.Exams;
using Nirengi.Modules.Curriculum.Domain.Topics;

namespace Nirengi.Modules.Curriculum.Infrastructure;

public sealed class CurriculumDbContext(DbContextOptions<CurriculumDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "curriculum";

    public DbSet<ExamTest> Tests => Set<ExamTest>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<ScoreType> ScoreTypes => Set<ScoreType>();

    public DbSet<Topic> Topics => Set<Topic>();

    public DbSet<TopicWeight> TopicWeights => Set<TopicWeight>();

    public DbSet<CurriculumRelease> Releases => Set<CurriculumRelease>();

    protected override string Schema => SchemaName;
}
