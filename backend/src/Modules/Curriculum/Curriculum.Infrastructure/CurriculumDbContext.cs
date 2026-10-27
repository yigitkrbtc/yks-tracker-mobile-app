using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;

namespace Nirengi.Modules.Curriculum.Infrastructure;

public sealed class CurriculumDbContext(DbContextOptions<CurriculumDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "curriculum";

    protected override string Schema => SchemaName;
}
