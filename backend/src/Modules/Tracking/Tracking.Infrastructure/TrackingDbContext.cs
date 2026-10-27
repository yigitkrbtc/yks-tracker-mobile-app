using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;

namespace Nirengi.Modules.Tracking.Infrastructure;

public sealed class TrackingDbContext(DbContextOptions<TrackingDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "tracking";

    protected override string Schema => SchemaName;
}
