using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Platform.Contracts;
using Nirengi.Modules.Platform.Infrastructure.Persistence.SyncEpochs;

namespace Nirengi.Modules.Platform.Infrastructure;

public static class PlatformInfrastructureRegistration
{
    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleInfrastructure<PlatformDbContext>(configuration, PlatformDbContext.SchemaName, Application.AssemblyReference.Assembly, publishesEvents: false);
        services.AddScoped<ISyncEpoch, SyncEpoch>();
        return services;
    }
}
