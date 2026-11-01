using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nirengi.BuildingBlocks.Application.Export;
using Nirengi.BuildingBlocks.Infrastructure;
using Nirengi.Modules.Tracking.Application.Abstractions;
using Nirengi.Modules.Tracking.Contracts;
using Nirengi.Modules.Tracking.Infrastructure.Persistence;

namespace Nirengi.Modules.Tracking.Infrastructure;

public static class TrackingInfrastructureRegistration
{
    public static IServiceCollection AddTrackingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleInfrastructure<TrackingDbContext>(configuration, TrackingDbContext.SchemaName, Application.AssemblyReference.Assembly);
        services.AddScoped<IStudyProfileRepository, StudyProfileRepository>();
        services.AddScoped<ITopicTestRepository, TopicTestRepository>();
        services.AddScoped<IMockExamRepository, MockExamRepository>();
        services.AddScoped<ITopicProgressRepository, TopicProgressRepository>();
        services.AddScoped<ITrackingUnitOfWork, TrackingUnitOfWork>();
        services.AddScoped<ITrackingActivityReader, TrackingActivityReader>();
        services.AddScoped<ITrackingAnalyticsReader, TrackingAnalyticsReader>();
        services.AddScoped<IPersonalDataExporter, TrackingDataExporter>();
        return services;
    }
}
