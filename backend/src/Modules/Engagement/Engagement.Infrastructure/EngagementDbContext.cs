using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;

namespace Nirengi.Modules.Engagement.Infrastructure;

public sealed class EngagementDbContext(DbContextOptions<EngagementDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "engagement";

    protected override string Schema => SchemaName;
}
