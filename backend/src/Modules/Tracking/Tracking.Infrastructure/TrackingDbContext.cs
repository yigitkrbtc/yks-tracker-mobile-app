using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Nirengi.Modules.Tracking.Domain.Profiles;
using Nirengi.Modules.Tracking.Domain.TopicTests;

namespace Nirengi.Modules.Tracking.Infrastructure;

public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "tracking";

    public DbSet<StudyProfile> StudyProfiles => Set<StudyProfile>();

    /// <summary>Feeds <see cref="TopicTest.ChangeSeq"/>; one sequence for every syncable Tracking record.</summary>
    public const string ChangeSequenceName = "change_seq";

    public DbSet<TopicTest> TopicTests => Set<TopicTest>();

    protected override string Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasSequence<long>(ChangeSequenceName);
    }
}
