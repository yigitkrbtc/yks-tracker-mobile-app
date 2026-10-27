using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nirengi.BuildingBlocks.Api.Modules;

/// <summary>Entry point the Host uses to compose a module; one per module Api project.</summary>
public interface IModule
{
    string Name { get; }

    void AddServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps the module's endpoints under the shared <c>/v1</c> group.</summary>
    void MapEndpoints(IEndpointRouteBuilder v1);
}
