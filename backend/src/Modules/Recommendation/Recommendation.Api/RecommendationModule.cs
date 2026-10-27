using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Api.Modules;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Recommendation.Infrastructure;

namespace Nirengi.Modules.Recommendation.Api;

public sealed class RecommendationModule : IModule
{
    public string Name => "Recommendation";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleInfrastructure<RecommendationDbContext>(
            configuration,
            RecommendationDbContext.SchemaName,
            Application.AssemblyReference.Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
    }
}
