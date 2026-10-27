using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Api.Modules;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Platform.Infrastructure;

namespace Nirengi.Modules.Platform.Api;

public sealed class PlatformModule : IModule
{
    public string Name => "Platform";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleInfrastructure<PlatformDbContext>(
            configuration,
            PlatformDbContext.SchemaName,
            Application.AssemblyReference.Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
    }
}
