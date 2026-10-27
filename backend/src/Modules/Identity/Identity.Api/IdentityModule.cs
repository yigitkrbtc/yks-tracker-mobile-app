using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Api.Modules;
using Nirengi.Modules.Identity.Api.Endpoints;
using Nirengi.Modules.Identity.Infrastructure;

namespace Nirengi.Modules.Identity.Api;

public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddIdentityInfrastructure(configuration);

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
        v1.MapAuthEndpoints();
        v1.MapMeEndpoints();
        v1.MapAccountEndpoints();
        v1.MapMfaEndpoints();
    }
}
