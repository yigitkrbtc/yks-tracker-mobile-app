using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Api.Modules;
using Nirengi.Modules.Analytics.Api.Endpoints;
using Nirengi.Modules.Analytics.Infrastructure;

namespace Nirengi.Modules.Analytics.Api;

public sealed class AnalyticsModule : IModule
{
    public string Name => "Analytics";

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddAnalyticsInfrastructure(configuration);

    public void MapEndpoints(IEndpointRouteBuilder v1) => v1.MapAnalyticsEndpoints();
}
