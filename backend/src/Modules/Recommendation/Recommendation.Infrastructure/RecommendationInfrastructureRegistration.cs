using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Application.Export;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Recommendation.Application;
using Nirengi.Modules.Recommendation.Application.Abstractions;
using Nirengi.Modules.Recommendation.Infrastructure.Persistence;

namespace Nirengi.Modules.Recommendation.Infrastructure;

public static class RecommendationInfrastructureRegistration
{
    public static IServiceCollection AddRecommendationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleInfrastructure<RecommendationDbContext>(configuration, RecommendationDbContext.SchemaName, Application.AssemblyReference.Assembly);
        services.AddRecommendationApplication();
        services.AddScoped<IRecommendationStore, RecommendationStore>();
        services.AddScoped<IPersonalDataExporter, RecommendationDataExporter>();
        return services;
    }
}
