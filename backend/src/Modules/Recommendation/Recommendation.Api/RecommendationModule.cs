using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Api.Modules;
using Nirengi.Modules.Recommendation.Api.Endpoints;
using Nirengi.Modules.Recommendation.Infrastructure;

namespace Nirengi.Modules.Recommendation.Api;

public sealed class RecommendationModule : IModule
{
    public string Name => "Recommendation";

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddRecommendationInfrastructure(configuration);

    public void MapEndpoints(IEndpointRouteBuilder v1) => v1.MapRecommendationEndpoints();
}
