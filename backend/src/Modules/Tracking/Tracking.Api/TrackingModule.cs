using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Api.Modules;
using Nirengi.Modules.Tracking.Api.Endpoints;
using Nirengi.Modules.Tracking.Infrastructure;

namespace Nirengi.Modules.Tracking.Api;

public sealed class TrackingModule : IModule
{
    public string Name => "Tracking";

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddTrackingInfrastructure(configuration);

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
        v1.MapProfileEndpoints();
        v1.MapTopicTestEndpoints();
        v1.MapMockExamEndpoints();
        v1.MapSyncEndpoints();
    }
}
