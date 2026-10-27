using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;

namespace Nirengi.Modules.Analytics.Infrastructure;

public sealed class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "analytics";

    protected override string Schema => SchemaName;
}
