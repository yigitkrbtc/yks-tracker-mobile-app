using System.Reflection;

namespace Nirengi.ArchitectureTests;

public sealed record ModuleAssemblies(
    string Name,
    Assembly Contracts,
    Assembly Core,
    Assembly Application,
    Assembly Infrastructure,
    Assembly Api)
{
    public string Namespace => $"Nirengi.Modules.{Name}";

    public IEnumerable<Assembly> All => [Contracts, Core, Application, Infrastructure, Api];

    public override string ToString() => Name;

    public static readonly IReadOnlyList<ModuleAssemblies> Catalog =
    [
        new ModuleAssemblies(
            "Identity",
            Modules.Identity.Contracts.AssemblyReference.Assembly,
            Modules.Identity.Domain.AssemblyReference.Assembly,
            Modules.Identity.Application.AssemblyReference.Assembly,
            Modules.Identity.Infrastructure.AssemblyReference.Assembly,
            typeof(Modules.Identity.Api.IdentityModule).Assembly),
        new ModuleAssemblies(
            "Curriculum",
            Modules.Curriculum.Contracts.AssemblyReference.Assembly,
            Modules.Curriculum.Domain.AssemblyReference.Assembly,
            Modules.Curriculum.Application.AssemblyReference.Assembly,
            Modules.Curriculum.Infrastructure.AssemblyReference.Assembly,
            typeof(Modules.Curriculum.Api.CurriculumModule).Assembly),
        new ModuleAssemblies(
            "Tracking",
            Modules.Tracking.Contracts.AssemblyReference.Assembly,
            Modules.Tracking.Domain.AssemblyReference.Assembly,
            Modules.Tracking.Application.AssemblyReference.Assembly,
            Modules.Tracking.Infrastructure.AssemblyReference.Assembly,
            typeof(Modules.Tracking.Api.TrackingModule).Assembly),
        new ModuleAssemblies(
            "Analytics",
            Modules.Analytics.Contracts.AssemblyReference.Assembly,
            Modules.Analytics.Engine.AssemblyReference.Assembly,
            Modules.Analytics.Application.AssemblyReference.Assembly,
            Modules.Analytics.Infrastructure.AssemblyReference.Assembly,
            typeof(Modules.Analytics.Api.AnalyticsModule).Assembly),
        new ModuleAssemblies(
            "Recommendation",
            Modules.Recommendation.Contracts.AssemblyReference.Assembly,
            Modules.Recommendation.Domain.AssemblyReference.Assembly,
            Modules.Recommendation.Application.AssemblyReference.Assembly,
            Modules.Recommendation.Infrastructure.AssemblyReference.Assembly,
            typeof(Modules.Recommendation.Api.RecommendationModule).Assembly),
        new ModuleAssemblies(
            "Engagement",
            Modules.Engagement.Contracts.AssemblyReference.Assembly,
            Modules.Engagement.Domain.AssemblyReference.Assembly,
            Modules.Engagement.Application.AssemblyReference.Assembly,
            Modules.Engagement.Infrastructure.AssemblyReference.Assembly,
            typeof(Modules.Engagement.Api.EngagementModule).Assembly),
        new ModuleAssemblies(
            "Platform",
            Modules.Platform.Contracts.AssemblyReference.Assembly,
            Modules.Platform.Domain.AssemblyReference.Assembly,
            Modules.Platform.Application.AssemblyReference.Assembly,
            Modules.Platform.Infrastructure.AssemblyReference.Assembly,
            typeof(Modules.Platform.Api.PlatformModule).Assembly),
    ];

    public static TheoryData<string> Names() => new(Catalog.Select(m => m.Name));

    public static ModuleAssemblies Get(string name) => Catalog.Single(m => m.Name == name);
}
