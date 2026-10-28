using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Infrastructure.Persistence;

namespace Nirengi.Modules.Tracking.Infrastructure;

public static class TrackingInfrastructureRegistration
{
    public static IServiceCollection AddTrackingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleInfrastructure<TrackingDbContext>(configuration, TrackingDbContext.SchemaName, Application.AssemblyReference.Assembly);
        services.AddScoped<IStudyProfileRepository, StudyProfileRepository>();
        services.AddScoped<ITrackingUnitOfWork, TrackingUnitOfWork>();
        return services;
    }
}
