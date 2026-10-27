using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Api.Modules;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Identity.Infrastructure;

namespace Nirengi.Modules.Identity.Api;

public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleInfrastructure<IdentityDbContext>(
            configuration,
            IdentityDbContext.SchemaName,
            Application.AssemblyReference.Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
    }
}
