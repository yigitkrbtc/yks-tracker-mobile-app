using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;

namespace Nirengi.Modules.Platform.Infrastructure;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "platform";

    protected override string Schema => SchemaName;
}
