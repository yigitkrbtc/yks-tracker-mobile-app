using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;

namespace Nirengi.Modules.Recommendation.Infrastructure;

public sealed class RecommendationDbContext(DbContextOptions<RecommendationDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "recommendation";

    protected override string Schema => SchemaName;
}
