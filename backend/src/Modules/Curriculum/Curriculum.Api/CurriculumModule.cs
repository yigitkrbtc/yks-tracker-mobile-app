using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Api.Modules;
using Nirengi.Modules.Curriculum.Api.Endpoints;
using Nirengi.Modules.Curriculum.Infrastructure;

namespace Nirengi.Modules.Curriculum.Api;

public sealed class CurriculumModule : IModule
{
    public string Name => "Curriculum";

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddCurriculumInfrastructure(configuration);

    public void MapEndpoints(IEndpointRouteBuilder v1) => v1.MapCurriculumEndpoints();
}
