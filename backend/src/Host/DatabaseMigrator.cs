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
    /// <summary>
    /// Applies every module's pending migrations (each module keeps its own history table in its schema), then
    /// loads reference data that ships with the code, so a migrated database is always a usable one.
    /// </summary>
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

        await scope.ServiceProvider.GetRequiredService<CurriculumSeeder>().Seed(cancellationToken);
    }

    private static Task Migrate<TContext>(IServiceProvider services, CancellationToken cancellationToken)
        where TContext : DbContext =>
        services.GetRequiredService<TContext>().Database.MigrateAsync(cancellationToken);
}
