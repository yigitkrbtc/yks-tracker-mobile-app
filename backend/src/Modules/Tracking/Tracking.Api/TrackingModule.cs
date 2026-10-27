using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Api.Modules;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Tracking.Infrastructure;

namespace Nirengi.Modules.Tracking.Api;

public sealed class TrackingModule : IModule
{
    public string Name => "Tracking";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleInfrastructure<TrackingDbContext>(
            configuration,
            TrackingDbContext.SchemaName,
            Application.AssemblyReference.Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
    }
}
