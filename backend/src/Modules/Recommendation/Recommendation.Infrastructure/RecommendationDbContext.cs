using Microsoft.EntityFrameworkCore;
using Nirengi.BuildingBlocks.Infrastructure.Persistence;
using Nirengi.Modules.Recommendation.Domain.Plans;

namespace Nirengi.Modules.Recommendation.Infrastructure;

public sealed class RecommendationDbContext(DbContextOptions<RecommendationDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "recommendation";

    public DbSet<StoredPlan> Plans => Set<StoredPlan>();

    public DbSet<ItemFeedback> Feedback => Set<ItemFeedback>();

    protected override string Schema => SchemaName;
}
