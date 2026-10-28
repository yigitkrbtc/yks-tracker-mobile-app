using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Nirengi.Modules.Tracking.Domain.Profiles;

namespace Nirengi.Modules.Tracking.Infrastructure;

public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "tracking";

    public DbSet<StudyProfile> StudyProfiles => Set<StudyProfile>();

    protected override string Schema => SchemaName;
}
