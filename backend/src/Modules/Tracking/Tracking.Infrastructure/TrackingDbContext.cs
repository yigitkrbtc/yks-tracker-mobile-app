using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Nirengi.Modules.Tracking.Domain.MockExams;
using Nirengi.Modules.Tracking.Domain.Profiles;
using Nirengi.Modules.Tracking.Domain.Progress;
using Nirengi.Modules.Tracking.Domain.TopicTests;

namespace Nirengi.Modules.Tracking.Infrastructure;

public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "tracking";

    public DbSet<StudyProfile> StudyProfiles => Set<StudyProfile>();

    /// <summary>Feeds <see cref="Domain.ISyncRecord.ChangeSeq"/>; one sequence for every syncable Tracking record.</summary>
    public const string ChangeSequenceName = "change_seq";

    public DbSet<TopicTest> TopicTests => Set<TopicTest>();

    public DbSet<MockExam> MockExams => Set<MockExam>();

    public DbSet<TopicProgress> TopicProgress => Set<TopicProgress>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasSequence<long>(ChangeSequenceName);
    }
}
