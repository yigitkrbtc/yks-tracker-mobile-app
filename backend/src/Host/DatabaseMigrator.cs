using Microsoft.EntityFrameworkCore;
using Nirengi.Modules.Identity.Infrastructure;
using Nirengi.Modules.Curriculum.Infrastructure;
using Nirengi.Modules.Curriculum.Infrastructure.Seed;
using Nirengi.Modules.Tracking.Infrastructure;
using Nirengi.Modules.Analytics.Infrastructure;
using Nirengi.Modules.Recommendation.Infrastructure;
using Nirengi.Modules.Engagement.Infrastructure;
using Nirengi.Modules.Platform.Infrastructure;

namespace Nirengi.Host;

internal static class DatabaseMigrator
{
    /// <summary>Applies every module's pending migrations. Each module keeps its own history table in its schema.</summary>
    public static async Task MigrateAll(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await Migrate<IdentityDbContext>(scope.ServiceProvider, cancellationToken);
        await Migrate<CurriculumDbContext>(scope.ServiceProvider, cancellationToken);
        await Migrate<TrackingDbContext>(scope.ServiceProvider, cancellationToken);
        await Migrate<AnalyticsDbContext>(scope.ServiceProvider, cancellationToken);
        await Migrate<RecommendationDbContext>(scope.ServiceProvider, cancellationToken);
        await Migrate<EngagementDbContext>(scope.ServiceProvider, cancellationToken);
        await Migrate<PlatformDbContext>(scope.ServiceProvider, cancellationToken);
    }

    /// <summary>
    /// Loads reference data that ships with the code (a no-op when unchanged). Runs on every start, also where
    /// migrations are applied out of band, so a deploy never serves an empty curriculum.
    /// </summary>
    public static async Task SeedReferenceData(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CurriculumSeeder>().Seed(cancellationToken);
    }

    private static Task Migrate<TContext>(IServiceProvider services, CancellationToken cancellationToken)
        where TContext : DbContext =>
        services.GetRequiredService<TContext>().Database.MigrateAsync(cancellationToken);
}
