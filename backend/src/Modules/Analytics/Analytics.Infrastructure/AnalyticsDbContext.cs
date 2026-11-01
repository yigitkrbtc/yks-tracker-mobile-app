using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Nirengi.Modules.Analytics.Application.Snapshots;

namespace Nirengi.Modules.Analytics.Infrastructure;

public sealed class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "analytics";

    public DbSet<UserAnalysis> UserAnalyses => Set<UserAnalysis>();

    public DbSet<TopicSnapshot> TopicSnapshots => Set<TopicSnapshot>();

    public DbSet<SectionSnapshot> SectionSnapshots => Set<SectionSnapshot>();

    protected override string Schema => SchemaName;
}
