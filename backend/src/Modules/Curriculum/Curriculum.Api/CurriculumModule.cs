using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Api.Modules;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Curriculum.Infrastructure;

namespace Nirengi.Modules.Curriculum.Api;

public sealed class CurriculumModule : IModule
{
    public string Name => "Curriculum";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleInfrastructure<CurriculumDbContext>(
            configuration,
            CurriculumDbContext.SchemaName,
            Application.AssemblyReference.Assembly);
    }

    public void MapEndpoints(IEndpointRouteBuilder v1)
    {
    }
}
