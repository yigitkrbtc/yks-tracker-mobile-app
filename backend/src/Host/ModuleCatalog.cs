using Nirengi.BuildingBlocks.Api.Modules;
using Nirengi.Modules.Identity.Api;
using Nirengi.Modules.Curriculum.Api;
using Nirengi.Modules.Tracking.Api;
using Nirengi.Modules.Analytics.Api;
using Nirengi.Modules.Recommendation.Api;
using Nirengi.Modules.Engagement.Api;
using Nirengi.Modules.Platform.Api;

namespace Nirengi.Host;

/// <summary>The modules this host runs. Order is irrelevant; modules never call each other directly.</summary>
internal static class ModuleCatalog
{
    public static readonly IReadOnlyList<IModule> All =
    [
        new IdentityModule(),
        new CurriculumModule(),
        new TrackingModule(),
        new AnalyticsModule(),
        new RecommendationModule(),
        new EngagementModule(),
        new PlatformModule(),
    ];
}
